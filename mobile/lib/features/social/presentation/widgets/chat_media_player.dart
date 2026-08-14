import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:just_audio/just_audio.dart';
import 'package:video_player/video_player.dart';

import '../../../../core/theme/design_tokens.dart';
import '../../domain/entities/chat_attachment.dart';
import '../providers/chat_providers.dart';

/// Inline playback for voice notes and video (D-298).
///
/// The signed URL is minted **when the reader presses play**, not when the bubble renders. Two
/// reasons, and both matter: signatures expire and the server re-checks room membership on every
/// mint, so a URL fetched at render time would be stale by the time anyone used it; and a room with
/// twenty voice notes would otherwise issue twenty mints just to scroll past them.
class ChatAudioPlayer extends ConsumerStatefulWidget {
  const ChatAudioPlayer({super.key, required this.attachment});

  final ChatAttachment attachment;

  @override
  ConsumerState<ChatAudioPlayer> createState() => _ChatAudioPlayerState();
}

class _ChatAudioPlayerState extends ConsumerState<ChatAudioPlayer> {
  final _player = AudioPlayer();
  bool _loaded = false;
  bool _failed = false;

  @override
  void dispose() {
    _player.dispose();
    super.dispose();
  }

  Future<void> _toggle() async {
    if (_player.playing) {
      await _player.pause();
      return;
    }
    if (!_loaded) {
      try {
        final url = await ref.read(chatRepositoryProvider).attachmentUrl(widget.attachment.id);
        await _player.setUrl(url);
        _loaded = true;
      } catch (_) {
        if (mounted) setState(() => _failed = true);
        return;
      }
    }
    // Replay rather than sit at the end: pressing play on a finished note should play it again.
    if (_player.processingState == ProcessingState.completed) await _player.seek(Duration.zero);
    await _player.play();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    if (_failed) {
      return Text('Voice note unavailable', style: TextStyle(color: c.muted, fontSize: 12));
    }

    return Container(
      constraints: const BoxConstraints(maxWidth: 260),
      padding: const EdgeInsets.symmetric(horizontal: KSpace.sm, vertical: KSpace.xs),
      decoration: BoxDecoration(
        color: c.elevated,
        borderRadius: BorderRadius.circular(KRadius.md),
        border: Border.all(color: c.border),
      ),
      child: StreamBuilder<PlayerState>(
        stream: _player.playerStateStream,
        builder: (context, snapshot) {
          final playing = snapshot.data?.playing ?? false;
          return Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              IconButton(
                tooltip: playing ? 'Pause voice note' : 'Play voice note',
                icon: Icon(playing ? Icons.pause_rounded : Icons.play_arrow_rounded),
                onPressed: _toggle,
              ),
              Expanded(
                child: StreamBuilder<Duration>(
                  stream: _player.positionStream,
                  builder: (context, pos) {
                    final total = _player.duration ?? Duration.zero;
                    final at = pos.data ?? Duration.zero;
                    final fraction = total.inMilliseconds == 0
                        ? 0.0
                        : (at.inMilliseconds / total.inMilliseconds).clamp(0.0, 1.0);
                    return Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        LinearProgressIndicator(value: fraction, minHeight: 3),
                        const SizedBox(height: 2),
                        Text(
                          '${_clock(at)} / ${_clock(total)}',
                          style: TextStyle(color: c.muted, fontSize: 11),
                        ),
                      ],
                    );
                  },
                ),
              ),
            ],
          );
        },
      ),
    );
  }
}

/// Video, same minting rule as audio: the URL is fetched on the first press, not on render.
class ChatVideoPlayer extends ConsumerStatefulWidget {
  const ChatVideoPlayer({super.key, required this.attachment});

  final ChatAttachment attachment;

  @override
  ConsumerState<ChatVideoPlayer> createState() => _ChatVideoPlayerState();
}

class _ChatVideoPlayerState extends ConsumerState<ChatVideoPlayer> {
  VideoPlayerController? _controller;
  bool _loading = false;
  bool _failed = false;

  @override
  void dispose() {
    _controller?.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    if (_loading || _controller != null) return;
    setState(() => _loading = true);
    try {
      final url = await ref.read(chatRepositoryProvider).attachmentUrl(widget.attachment.id);
      final controller = VideoPlayerController.networkUrl(Uri.parse(url));
      await controller.initialize();
      if (!mounted) {
        await controller.dispose();
        return;
      }
      setState(() {
        _controller = controller;
        _loading = false;
      });
      await controller.play();
    } catch (_) {
      if (mounted) setState(() => _failed = true);
    }
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    if (_failed) return Text('Video unavailable', style: TextStyle(color: c.muted, fontSize: 12));

    final controller = _controller;
    if (controller == null) {
      return Semantics(
        button: true,
        label: 'Play video ${widget.attachment.fileName}',
        child: InkWell(
          onTap: _load,
          child: Container(
            width: 220,
            height: 124,
            decoration: BoxDecoration(
              color: c.elevated,
              borderRadius: BorderRadius.circular(KRadius.md),
              border: Border.all(color: c.border),
            ),
            child: Center(
              child: _loading
                  ? const CircularProgressIndicator()
                  : Icon(Icons.play_circle_outline_rounded, size: 40, color: c.muted),
            ),
          ),
        ),
      );
    }

    return ClipRRect(
      borderRadius: BorderRadius.circular(KRadius.md),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 260),
        child: AspectRatio(
          aspectRatio: controller.value.aspectRatio,
          child: Stack(
            alignment: Alignment.bottomCenter,
            children: [
              VideoPlayer(controller),
              VideoProgressIndicator(controller, allowScrubbing: true),
              // Tap-to-toggle over the surface, so the whole frame is the control rather than a
              // small overlay button that is hard to hit on a phone.
              Positioned.fill(
                child: Semantics(
                  button: true,
                  label: 'Play or pause video',
                  child: GestureDetector(
                    onTap: () => setState(() =>
                        controller.value.isPlaying ? controller.pause() : controller.play()),
                    child: const SizedBox.expand(),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

String _clock(Duration d) =>
    '${d.inMinutes}:${(d.inSeconds % 60).toString().padLeft(2, '0')}';
