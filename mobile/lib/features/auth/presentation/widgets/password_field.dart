import 'package:flutter/material.dart';

import '../../../../core/theme/design_tokens.dart';
import '../password_policy.dart';

/// Password input with an optional real-time strength meter. The meter is a soft hint only (the
/// backend has no composition rules, D-129) — length is what gates submission, not this. The parent
/// rebuilds on [onChanged], so the meter reflects the current text.
class PasswordField extends StatelessWidget {
  const PasswordField({
    super.key,
    required this.controller,
    required this.label,
    this.showStrength = false,
    this.onChanged,
    this.textInputAction,
    this.onSubmitted,
    this.autofillHints,
  });

  final TextEditingController controller;
  final String label;
  final bool showStrength;
  final ValueChanged<String>? onChanged;
  final TextInputAction? textInputAction;
  final VoidCallback? onSubmitted;
  final Iterable<String>? autofillHints;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    final strength = showStrength ? passwordStrength(controller.text) : null;
    final barColor = switch (strength?.score ?? 0) {
      1 || 2 => c.danger,
      >= 3 => c.accent,
      _ => c.border,
    };

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      mainAxisSize: MainAxisSize.min,
      children: [
        TextField(
          controller: controller,
          obscureText: true,
          autofillHints: autofillHints,
          textInputAction: textInputAction,
          onChanged: onChanged,
          onSubmitted: onSubmitted == null ? null : (_) => onSubmitted!(),
          decoration: InputDecoration(labelText: label),
        ),
        if (strength != null && controller.text.isNotEmpty) ...[
          const SizedBox(height: KSpace.sm),
          Row(
            children: [
              for (var segment = 1; segment <= 4; segment++) ...[
                Expanded(
                  child: Container(
                    height: 4,
                    decoration: BoxDecoration(
                      color: segment <= strength.score ? barColor : c.border,
                      borderRadius: BorderRadius.circular(2),
                    ),
                  ),
                ),
                if (segment < 4) const SizedBox(width: 4),
              ],
              const SizedBox(width: KSpace.sm),
              SizedBox(
                width: 44,
                child: Text(strength.label,
                    textAlign: TextAlign.right,
                    style: TextStyle(color: c.muted, fontSize: 12, fontWeight: FontWeight.w600)),
              ),
            ],
          ),
        ],
      ],
    );
  }
}
