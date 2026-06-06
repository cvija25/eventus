import 'dart:async';
import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;
import '../models/item.dart';

class ApiService {
  static String get _host =>
      !kIsWeb && defaultTargetPlatform == TargetPlatform.android
          ? '10.0.2.2'
          : 'localhost';

  static String get _baseUrl {
    return 'http://$_host:8081/api/v1/catalog/events';
  }

  static String get _accountUrl => 'http://$_host:8080/api/v1/account';

  Future<List<Item>> fetchItems() async {
    final uri = Uri.parse(_baseUrl);

    try {
      final response = await http.get(uri).timeout(const Duration(seconds: 10));

      if (response.statusCode == 200) {
        final decoded = jsonDecode(response.body);
        if (decoded is! List) {
          throw Exception('Expected a list of events from $uri');
        }

        return decoded
            .map((json) => Item.fromJson(json as Map<String, dynamic>))
            .toList();
      }

      throw Exception('GET $uri failed with status ${response.statusCode}');
    } on TimeoutException {
      throw Exception('GET $uri timed out');
    } on FormatException catch (error) {
      throw Exception('GET $uri returned invalid JSON: ${error.message}');
    } on http.ClientException catch (error) {
      if (kIsWeb) {
        throw Exception(
          'GET $uri failed in Chrome: ${error.message}. '
          'Open the URL in Chrome to confirm the backend is running. '
          'If it opens there, enable CORS on the backend for the Flutter web origin.',
        );
      }

      throw Exception('GET $uri failed: ${error.message}');
    }
  }

  Future<void> createAccountStake({
    required String id,
    required String ownerId,
    required int stake,
  }) async {
    final uri = Uri.parse(_accountUrl);

    try {
      final response = await http
          .post(
            uri,
            headers: const {'Content-Type': 'application/json'},
            body: jsonEncode({
              'eventId': id,
              'ownerId': ownerId,
              'stake': stake,
            }),
          )
          .timeout(const Duration(seconds: 10));

      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw Exception('POST $uri failed with status ${response.statusCode}');
      }
    } on TimeoutException {
      throw Exception('POST $uri timed out');
    } on http.ClientException catch (error) {
      if (kIsWeb) {
        throw Exception(
          'POST $uri failed in Chrome: ${error.message}. '
          'If the endpoint works directly, enable CORS on the backend for the Flutter web origin.',
        );
      }

      throw Exception('POST $uri failed: ${error.message}');
    }
  }
}
