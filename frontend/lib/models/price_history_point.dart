// lib/models/price_history_point.dart
class PriceHistoryPoint {
  final String eventId;
  final double priceYes;
  final double priceNo;
  final DateTime timestamp;

  PriceHistoryPoint({
    required this.eventId,
    required this.priceYes,
    required this.priceNo,
    required this.timestamp,
  });

  factory PriceHistoryPoint.fromJson(Map<String, dynamic> json) {
    double toDouble(dynamic v) {
      if (v is num) return v.toDouble();
      return double.tryParse(v?.toString() ?? '') ?? 0.0;
    }

    return PriceHistoryPoint(
      eventId: (json['eventId'] ?? json['event_id'] ?? '').toString(),
      priceYes: toDouble(json['priceYes'] ?? json['price_yes']),
      priceNo: toDouble(json['priceNo'] ?? json['price_no']),
      timestamp: DateTime.tryParse(
            (json['timestamp'] ?? json['Timestamp'] ?? '').toString(),
          ) ??
          DateTime.now(),
    );
  }
}