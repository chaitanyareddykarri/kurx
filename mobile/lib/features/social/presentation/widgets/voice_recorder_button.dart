import 'dart:async';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:record/record.dart';

import '../../../../core/theme/design_tokens.dart';

/// Voice notes (D-298).
///
/// Records to a temp file with `record`, then hands the bytes to the SAME attachment pipeline as any
/// other file — the server's size, MIME, magic-byte and malware checks all apply unchanged. Nothing
/// about a recording is trusted because our own recorder produced it.
///
/// Opus in an Ogg container: it is the format the server already accepts, it is roughly a tenth the
/// size of the equivalent AAC for speech, and both platforms encode it natively.
///
/// The recorder is disposed on every exit path — send, discard, an error, the widget going away. A
/// live recorder holds the microphone, and an app that keeps the mic open after the user thinks they
/// stopped is the kind of thing people rightly never forgive.
class VoiceRecorderButton extends StatefulWidget {
  const VoiceRecorderButton({super.key, required this.onRecorded});

  /// Called once with the finished recording. The caller owns the upload.
  final void Function({
    required String fileName,
    required String contentType,
    required List<int> bytes,
    required String localPath,
  }) onRecorded;

  @override
  State<VoiceRecorderButton> createState() => _VoiceRecorderButtonState();
}

class _VoiceRecorderButtonState extends State<VoiceRecorderButton> {
  /// Five minutes. The upload has a size limit the server enforces, and discovering that after
  /// speaking for ten minutes is worse than being stopped at five.
  static const _maxDuration = Duration(minutes: 5);

  final _recorder = AudioRecorder();
  Timer? _ticker;
  Duration _elapsed = Duration.zero;
  bool _recording = false;
  String? _path;

  @override
  void dispose() {
    _ticker?.cancel();
    _recorder.dispose();
    super.dispose();
  }

  Future<void> _start() async {
    // The package asks the OS; declining leaves the rest of the composer working.
    if (!await _recorder.hasPermission()) {
      if (!mounted) return;
      ScaffoldMessenger.of(context)
          .showSnackBar(const SnackBar(content: Text('Microphone permission is needed')));
      return;
    }

    final dir = Directory.systemTemp;
    final path = '${dir.path}/voice-note-${DateTime.now().millisecondsSinceEpoch}.ogg';
    await _recorder.start(
      const RecordConfig(encoder: AudioEncoder.opus, sampleRate: 24000, numChannels: 1),
      path: path,
    );

    if (!mounted) {
      await _recorder.stop();
      return;
    }
    setState(() {
      _recording = true;
      _elapsed = Duration.zero;
      _path = path;
    });
    _ticker = Timer.periodic(const Duration(seconds: 1), (_) {
      if (!mounted) return;
      setState(() => _elapsed += const Duration(seconds: 1));
      if (_elapsed >= _maxDuration) _finish();
    });
  }

  Future<void> _finish() async {
    _ticker?.cancel();
    final path = await _recorder.stop() ?? _path;
    if (!mounted) return;
    setState(() => _recording = false);
    if (path == null) return;

    final file = File(path);
    if (!await file.exists()) return;
    final bytes = await file.readAsBytes();
    // A zero-length file is a tap, not a recording. Uploading it would cost a round trip to be told
    // the same thing by the server.
    if (bytes.isEmpty) return;

    widget.onRecorded(
      fileName: path.split(Platform.pathSeparator).last.split('/').last,
      contentType: 'audio/ogg',
      bytes: bytes,
      localPath: path,
    );
  }

  Future<void> _discard() async {
    _ticker?.cancel();
    final path = await _recorder.stop() ?? _path;
    if (path != null) {
      // Best effort: a leftover temp file is harmless, and the OS clears the directory anyway.
      try {
        await File(path).delete();
      } catch (_) {}
    }
    if (!mounted) return;
    setState(() => _recording = false);
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;

    if (!_recording) {
      return IconButton(
        tooltip: 'Record a voice note',
        icon: const Icon(Icons.mic_none_rounded),
        onPressed: _start,
      );
    }

    final mm = _elapsed.inMinutes;
    final ss = (_elapsed.inSeconds % 60).toString().padLeft(2, '0');
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        // The elapsed time IS the recording indicator: a red dot alone says nothing to a screen
        // reader and nothing at all to someone who cannot distinguish the colour.
        Semantics(
          liveRegion: true,
          child: Text('$mm:$ss', style: TextStyle(color: c.danger, fontSize: 12)),
        ),
        IconButton(
          tooltip: 'Discard recording',
          icon: const Icon(Icons.delete_outline_rounded),
          onPressed: _discard,
        ),
        IconButton(
          tooltip: 'Finish recording',
          icon: const Icon(Icons.stop_circle_outlined),
          onPressed: _finish,
        ),
      ],
    );
  }
}
