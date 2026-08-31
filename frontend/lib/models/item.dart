class Item {
  final String id;
  final String title;
  final String ownerId;
  final double priceYes;
  final double priceNo;
  final double potSize;
  final int? outcome;

  const Item({
    required this.id,
    required this.title,
    required this.ownerId,
    required this.priceYes,
    required this.priceNo,
    required this.potSize,
    this.outcome,
  });

  bool get isResolved => outcome != null;

  Item copyWith({
    String? id,
    String? title,
    String? ownerId,
    double? priceYes,
    double? priceNo,
    double? potSize,
    int? outcome,
  }) {
    return Item(
      id: id ?? this.id,
      title: title ?? this.title,
      ownerId: ownerId ?? this.ownerId,
      priceYes: priceYes ?? this.priceYes,
      priceNo: priceNo ?? this.priceNo,
      potSize: potSize ?? this.potSize,
      outcome: outcome ?? this.outcome,
    );
  }

  factory Item.fromJson(Map<String, dynamic> json) {
    final rawId = json['Id'] ?? json['id'];
    final rawTitle = json['Title'] ?? json['title'];
    final rawOwnerId = json['OwnerId'] ?? json['ownerId'];
    final rawPriceYes = json['PriceYes'] ?? json['priceYes'];
    final rawPriceNo = json['PriceNo'] ?? json['priceNo'];
    final rawPotSize = json['PotSize'] ?? json['potSize'];
    final rawOutcome = json['Outcome'] ?? json['outcome'];

    return Item(
      id: rawId?.toString() ?? '',
      title: rawTitle?.toString() ?? 'Untitled event',
      ownerId: rawOwnerId?.toString() ?? '',
      priceYes: _toDouble(rawPriceYes) ?? 0,
      priceNo: _toDouble(rawPriceNo) ?? 0,
      potSize: _toDouble(rawPotSize) ?? 0,
      outcome: rawOutcome is int ? rawOutcome : int.tryParse(rawOutcome?.toString() ?? ''),
    );
  }

  static double? _toDouble(Object? value) {
    if (value is double) return value;
    if (value is num) return value.toDouble();
    return double.tryParse(value?.toString() ?? '');
  }
}