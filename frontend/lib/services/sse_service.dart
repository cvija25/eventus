import 'dart:async';
import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;

class SseService {
  SseService._internal();
  static final SseService instance = SseService._internal();

  http.Client? _client;
  StreamSubscription<List<int>>? _subscription;
  final _controller = StreamController<Map<String, dynamic>>.broadcast();
  final _buffer = StringBuffer();

  Stream<Map<String, dynamic>> get priceStream => _controller.stream;

  String get _host =>
      !kIsWeb && defaultTargetPlatform == TargetPlatform.android
          ? '10.0.2.2'
          : 'localhost';

  int get _gatewayPort => 1234;

  Uri get _sseUri => Uri.parse('http://$_host:$_gatewayPort/catalog/api/v1/catalog/events/stream');

  Future<void> connect() async {
    if (_client != null) return;

    _client = http.Client();

    try {
      final req = http.Request('GET', _sseUri);
      final streamed = await _client!.send(req);
      debugPrint('SSE connected, status: ${streamed.statusCode}');

      if (streamed.statusCode != 200) {
        debugPrint('SSE non-OK status: ${streamed.statusCode}');
        _cleanup();
        return;
      }

      _subscription = streamed.stream.listen((chunk) {
        try {
          final text = utf8.decode(chunk);
          // small summary for logs
          final summary = text.length > 200 ? '${text.substring(0, 200)}...' : text;
          debugPrint('SSE chunk received (${text.length} bytes): ${summary.replaceAll('\n', '\\n')}');

          _buffer.write(text);
          var content = _buffer.toString();
          // split on CRLF-CRLF or LF-LF
          final splitter = RegExp(r'\r?\n\r?\n');
          final parts = content.split(splitter);
          // last part may be incomplete — keep it in buffer
          for (var i = 0; i < parts.length - 1; i++) {
            final raw = parts[i];
            _processEventBlock(raw);
          }
          final remaining = parts.isNotEmpty ? parts.last : '';
          _buffer.clear();
          _buffer.write(remaining);
        } catch (err, st) {
          debugPrint('SSE chunk parse error: $err\n$st');
        }
      }, onError: (err) {
        debugPrint('SSE stream error: $err');
        _cleanup();
      }, onDone: () {
        debugPrint('SSE stream done');
        _cleanup();
      }, cancelOnError: true);
    } catch (e, st) {
      debugPrint('SSE connect error: $e\n$st');
      _cleanup();
    }
  }

  void _cleanup() {
    _subscription?.cancel();
    _subscription = null;
    _client?.close();
    _client = null;
    _buffer.clear();
  }

  void dispose() {
    _cleanup();
    _controller.close();
  }

  void _processEventBlock(String block) {
    // SSE block may contain multiple `data:` lines; join them
    final lines = block.split('\n');
    final dataLines = <String>[];
    for (final line in lines) {
      if (line.startsWith('data:')) {
        dataLines.add(line.substring(5).trim());
      }
    }
    if (dataLines.isEmpty) return;
    final payload = dataLines.join('\n');
    try {
      debugPrint('SSE raw payload: $payload');
    } catch (_) {}
    try {
      final decoded = jsonDecode(payload);
      if (decoded is Map<String, dynamic>) {
        _controller.add(decoded);
      }
    } catch (_) {
      // ignore json errors
    }
  }
}
