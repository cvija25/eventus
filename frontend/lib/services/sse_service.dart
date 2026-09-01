import 'dart:async';
import 'dart:convert';
import 'package:http/http.dart' as http;
import 'api_routes.dart';

class SseService {
  SseService._internal();
  static final SseService instance = SseService._internal();

  http.Client? _client;
  StreamSubscription<List<int>>? _subscription;
  final _controller = StreamController<Map<String, dynamic>>.broadcast();
  final _buffer = StringBuffer();

  Stream<Map<String, dynamic>> get priceStream => _controller.stream;

  Future<void> connect() async {
    if (_client != null) return;

    _client = http.Client();

    try {
      final req = http.Request('GET', ApiRoutes.priceStream);
      final streamed = await _client!.send(req);

      if (streamed.statusCode != 200) {
        _cleanup();
        return;
      }

      _subscription = streamed.stream.listen((chunk) {
        try {
          final text = utf8.decode(chunk);

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
        } catch (_) {}
      }, onError: (_) {
        _cleanup();
      }, onDone: () {
        _cleanup();
      }, cancelOnError: true);
    } catch (_) {
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
      final decoded = jsonDecode(payload);
      if (decoded is Map<String, dynamic>) {
        _controller.add(decoded);
      }
    } catch (_) {
      // ignore json errors
    }
  }
}
