import 'package:flutter/material.dart';

/// Return ARGB integer for [color] without using deprecated members.
///
/// Parses the hex value from `color.toString()` which looks like
/// `Color(0xff00a3ff)` and returns the parsed integer. Falls back to
/// opaque black if parsing fails.
int argbFromColor(Color color) {
  final s = color.toString();
  final m = RegExp(r'0x[0-9a-fA-F]+').firstMatch(s);
  if (m != null) {
    try {
      return int.parse(m.group(0)!, radix: 16);
    } catch (_) {
      // fall through
    }
  }
  return 0xFF000000;
}
