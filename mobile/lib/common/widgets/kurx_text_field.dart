import 'package:flutter/material.dart';

import '../../core/theme/design_tokens.dart';

/// A labeled text field with a consistent label / helper / error stack, built
/// on the themed [InputDecorationTheme]. Use inside a [Form] with [validator]
/// for boundary validation; the field renders the error itself.
class KurxTextField extends StatelessWidget {
  const KurxTextField({
    super.key,
    required this.label,
    this.controller,
    this.hint,
    this.helper,
    this.prefixIcon,
    this.suffix,
    this.obscureText = false,
    this.keyboardType,
    this.maxLines = 1,
    this.enabled = true,
    this.validator,
    this.onChanged,
    this.textInputAction,
  });

  final String label;
  final TextEditingController? controller;
  final String? hint;
  final String? helper;
  final IconData? prefixIcon;
  final Widget? suffix;
  final bool obscureText;
  final TextInputType? keyboardType;
  final int maxLines;
  final bool enabled;
  final String? Function(String?)? validator;
  final ValueChanged<String>? onChanged;
  final TextInputAction? textInputAction;

  @override
  Widget build(BuildContext context) {
    final c = context.kurx;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(label, style: TextStyle(color: c.text, fontSize: 13, fontWeight: FontWeight.w700)),
        const SizedBox(height: KSpace.sm),
        TextFormField(
          controller: controller,
          obscureText: obscureText,
          keyboardType: keyboardType,
          maxLines: obscureText ? 1 : maxLines,
          enabled: enabled,
          validator: validator,
          onChanged: onChanged,
          textInputAction: textInputAction,
          style: TextStyle(color: c.text),
          decoration: InputDecoration(
            hintText: hint,
            helperText: helper,
            helperStyle: TextStyle(color: c.muted),
            prefixIcon: prefixIcon != null ? Icon(prefixIcon, color: c.muted, size: 20) : null,
            suffixIcon: suffix,
          ),
        ),
      ],
    );
  }
}
