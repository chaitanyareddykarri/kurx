import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

import '../../../../core/network/network_providers.dart';
import '../../../../core/network/api_guard.dart';
import '../../../../core/theme/design_tokens.dart';
import '../../../../core/theme/motion.dart';

class CheckinScannerPage extends ConsumerStatefulWidget {
  const CheckinScannerPage(
      {super.key, required this.orgId, required this.eventId});
  final String orgId;
  final String eventId;

  @override
  ConsumerState<CheckinScannerPage> createState() =>
      _CheckinScannerPageState();
}

class _CheckinScannerPageState extends ConsumerState<CheckinScannerPage> {
  final _controller = MobileScannerController();
  bool _processing = false;
  _ScanResult? _lastResult;
  int _checkedInCount = 0;

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Scaffold(
      backgroundColor: Colors.black,
      appBar: AppBar(
        backgroundColor: Colors.transparent,
        title: const Text(
          'Gate Scan',
          style: TextStyle(color: Colors.white),
        ),
        iconTheme: const IconThemeData(color: Colors.white),
        actions: [
          IconButton(
            // A working control with no name, on the scanner an operator uses at the door — often
            // in the dark, which is the whole point of the torch.
            tooltip: 'Toggle torch',
            icon: const Icon(Icons.flash_on_rounded, color: Colors.white),
            onPressed: () => _controller.toggleTorch(),
          ),
        ],
      ),
      extendBodyBehindAppBar: true,
      body: Stack(
        children: [
          // Camera
          MobileScanner(
            controller: _controller,
            onDetect: _onDetect,
          ),
          // Scan frame overlay
          CustomPaint(
            painter: _ScanFramePainter(borderColor: c.accent),
            child: const SizedBox.expand(),
          ),
          // Bottom info sheet
          Positioned(
            bottom: 0,
            left: 0,
            right: 0,
            child: Container(
              decoration: BoxDecoration(
                color: c.cardSurface.withValues(alpha: 0.95),
                borderRadius: const BorderRadius.vertical(
                    top: Radius.circular(KRadius.xl)),
              ),
              padding: const EdgeInsets.all(KSpace.xl),
              child: SafeArea(
                top: false,
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    if (_lastResult != null)
                      _ResultBanner(result: _lastResult!, c: c)
                    else
                      Text(
                        'Point camera at ticket QR code',
                        style: TextStyle(
                            color: c.muted, fontSize: 14),
                        textAlign: TextAlign.center,
                      ),
                    const SizedBox(height: KSpace.md),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(Icons.check_circle_rounded,
                            color: c.success, size: 18),
                        const SizedBox(width: KSpace.sm),
                        Text(
                          '$_checkedInCount checked in',
                          style: TextStyle(
                            color: c.text,
                            fontWeight: FontWeight.w700,
                            fontSize: 15,
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: KSpace.md),
                    TextButton(
                      onPressed: _showManualEntry,
                      child: Text(
                        'Enter code manually',
                        style: TextStyle(color: c.muted),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  Future<void> _onDetect(BarcodeCapture capture) async {
    if (_processing) return;
    final barcode = capture.barcodes.firstOrNull;
    if (barcode?.rawValue == null) return;
    await _checkIn(barcode!.rawValue!);
  }

  Future<void> _checkIn(String code) async {
    setState(() => _processing = true);
    try {
      final staff = isStaffPass(code);
      final res = await guard(() => ref.read(dioProvider).post(
            '/v1/gate/${widget.eventId}/${staff ? 'scan-staff' : 'scan'}',
            data: staff ? {'pass': code} : {'ticketCode': code},
          ));
      if (mounted) {
        setState(() {
          _checkedInCount++;
          // A marshal needs to read the badge's claim back against the person in front of them, so a
          // staff scan reports who and what rather than a bare "checked in".
          _lastResult = _ScanResult(success: true, message: staff ? staffLabel(res.data) : 'Checked in');
        });
      }
    } catch (_) {
      if (mounted) {
        setState(() {
          _lastResult = _ScanResult(
            success: false,
            message: isStaffPass(code) ? 'Invalid staff pass' : 'Invalid ticket',
          );
        });
      }
    } finally {
      if (mounted) {
        setState(() => _processing = false);
        await Future.delayed(const Duration(seconds: 2));
        if (mounted) setState(() => _lastResult = null);
      }
    }
  }

  void _showManualEntry() {
    final ctrl = TextEditingController();
    final c = context.kurx;
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: c.cardSurface,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(KRadius.xl)),
      ),
      builder: (_) => Padding(
        padding: EdgeInsets.fromLTRB(
          KSpace.lg,
          KSpace.lg,
          KSpace.lg,
          MediaQuery.viewInsetsOf(context).bottom + KSpace.lg,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Enter ticket code',
                style: TextStyle(
                    color: c.text,
                    fontWeight: FontWeight.w800,
                    fontSize: 17)),
            const SizedBox(height: KSpace.lg),
            TextField(
              controller: ctrl,
              textCapitalization: TextCapitalization.characters,
              decoration:
                  const InputDecoration(hintText: 'Ticket code / QR value'),
            ),
            const SizedBox(height: KSpace.lg),
            FilledButton(
              onPressed: () {
                Navigator.pop(context);
                if (ctrl.text.isNotEmpty) _checkIn(ctrl.text.trim());
              },
              child: const Text('Check In'),
            ),
          ],
        ),
      ),
    );
  }
}

/// A staff badge carries `staff:{assignmentId}:{signature}`; an attendee's carries a bare ticket code
/// (D-385). The two go to different routes rather than to one polymorphic endpoint, so neither
/// credential can be resolved as the other — and a scanner never has to guess which it is holding.
bool isStaffPass(String code) => code.startsWith('staff:');

/// What a staff scan puts on screen: the name the badge prints and what it authorises, so the two can be
/// read against the person holding it. Falls back to a plain confirmation when the response is not the
/// shape this build expects, rather than showing nothing.
String staffLabel(dynamic data) {
  if (data is! Map) return 'Staff checked in';
  final name = (data['name'] as String?)?.trim();
  final access = (data['access_level'] as String?)?.trim();
  if (name == null || name.isEmpty) return 'Staff checked in';
  return access == null || access.isEmpty ? name : '$name · $access';
}

class _ScanResult {
  const _ScanResult({required this.success, required this.message});
  final bool success;
  final String message;
}

class _ResultBanner extends StatelessWidget {
  const _ResultBanner({required this.result, required this.c});
  final _ScanResult result;
  final KurxColors c;

  @override
  Widget build(BuildContext context) {
    return AnimatedContainer(
      duration: context.motion(KMotion.base),
      padding: const EdgeInsets.all(KSpace.md),
      decoration: BoxDecoration(
        color: (result.success ? c.success : c.danger)
            .withValues(alpha: 0.15),
        borderRadius: BorderRadius.circular(KRadius.lg),
        border: Border.all(
          color: result.success ? c.success : c.danger,
          width: 1.5,
        ),
      ),
      child: Row(
        children: [
          Icon(
            result.success
                ? Icons.check_circle_rounded
                : Icons.cancel_rounded,
            color: result.success ? c.success : c.danger,
            size: 24,
          ),
          const SizedBox(width: KSpace.md),
          Text(
            result.message,
            style: TextStyle(
              color: result.success ? c.success : c.danger,
              fontWeight: FontWeight.w700,
              fontSize: 16,
            ),
          ),
        ],
      ),
    );
  }
}

class _ScanFramePainter extends CustomPainter {
  const _ScanFramePainter({required this.borderColor});
  final Color borderColor;

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = borderColor
      ..strokeWidth = 3
      ..style = PaintingStyle.stroke;

    const frameSize = 240.0;
    const cornerLen = 24.0;
    const radius = 8.0;
    final left = (size.width - frameSize) / 2;
    final top = (size.height - frameSize) / 3;
    final right = left + frameSize;
    final bottom = top + frameSize;

    // Top-left corner
    canvas.drawPath(
      Path()
        ..moveTo(left, top + cornerLen)
        ..lineTo(left, top + radius)
        ..arcToPoint(Offset(left + radius, top),
            radius: const Radius.circular(radius))
        ..lineTo(left + cornerLen, top),
      paint,
    );
    // Top-right corner
    canvas.drawPath(
      Path()
        ..moveTo(right - cornerLen, top)
        ..lineTo(right - radius, top)
        ..arcToPoint(Offset(right, top + radius),
            radius: const Radius.circular(radius))
        ..lineTo(right, top + cornerLen),
      paint,
    );
    // Bottom-left corner
    canvas.drawPath(
      Path()
        ..moveTo(left, bottom - cornerLen)
        ..lineTo(left, bottom - radius)
        ..arcToPoint(Offset(left + radius, bottom),
            radius: const Radius.circular(radius), clockwise: false)
        ..lineTo(left + cornerLen, bottom),
      paint,
    );
    // Bottom-right corner
    canvas.drawPath(
      Path()
        ..moveTo(right - cornerLen, bottom)
        ..lineTo(right - radius, bottom)
        ..arcToPoint(Offset(right, bottom - radius),
            radius: const Radius.circular(radius), clockwise: false)
        ..lineTo(right, bottom - cornerLen),
      paint,
    );

    // Dim overlay
    final dimPaint = Paint()..color = Colors.black.withValues(alpha: 0.5);
    canvas.drawPath(
      Path.combine(
        PathOperation.difference,
        Path()..addRect(Rect.fromLTWH(0, 0, size.width, size.height)),
        Path()
          ..addRRect(RRect.fromRectAndRadius(
            Rect.fromLTRB(left, top, right, bottom),
            const Radius.circular(radius),
          )),
      ),
      dimPaint,
    );
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}
