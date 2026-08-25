class Item {
  final String id;
  final String title;
  final String ownerId;
  final double priceYes;
  final double priceNo;
  final double potSizeYes;
  final double potSizeNo;
  final double potSize;
  final int? outcome;

  const Item({
    required this.id,
    required this.title,
    required this.ownerId,
    required this.priceYes,
    required this.priceNo,
    required this.potSizeYes,
    required this.potSizeNo,
    required this.potSize,
    this.outcome,
  });

  bool get isResolved => outcome != null;

  factory Item.fromJson(Map<String, dynamic> json) {
    final rawId = json['Id'] ?? json['id'];
    final rawTitle = json['Title'] ?? json['title'];
    final rawOwnerId = json['OwnerId'] ?? json['ownerId'];
    final rawPriceYes = json['PriceYes'] ?? json['priceYes'];
    final rawPriceNo = json['PriceNo'] ?? json['priceNo'];
    final rawPotSizeYes = json['PotSizeYes'] ?? json['potSizeYes'];
    final rawPotSizeNo = json['PotSizeNo'] ?? json['potSizeNo'];  
    final rawPotSize = json['PotSize'] ?? json['potSize'];
    final rawOutcome = json['Outcome'] ?? json['outcome'];

    return Item(
      id: rawId?.toString() ?? '',
      title: rawTitle?.toString() ?? 'Untitled event',
      ownerId: rawOwnerId?.toString() ?? '',
      priceYes: _toDouble(rawPriceYes) ?? 0,
      priceNo: _toDouble(rawPriceNo) ?? 0,
      potSizeYes: _toDouble(rawPotSizeYes) ?? 0,
      potSizeNo: _toDouble(rawPotSizeNo) ?? 0, 
      potSize: _toDouble(rawPotSize) ?? 0,
      outcome: rawOutcome is int ? rawOutcome : int.tryParse(rawOutcome?.toString() ?? ''),
    );
  }

  String get category {
    final categories = ['Politics', 'Crypto', 'Sports', 'Culture', 'Macro'];
    return categories[_stableNumber % categories.length];
  }

  String get closeLabel => isResolved ? 'Resolved' : '${3 + _stableNumber % 26} days left';

  String get changeLabel {
    final value = (_stableNumber * 3 % 17) - 8;
    if (value == 0) return 'flat';
    return '${value > 0 ? '+' : ''}$value%';
  }

  bool get isPositiveChange => !changeLabel.startsWith('-');

  int get liquidity => 12000 + (_stableNumber * 919 % 88000);

  int get _stableNumber => id.codeUnits.fold(0, (sum, unit) => sum + unit);

  static double? _toDouble(Object? value) {
    if (value is double) return value;
    if (value is num) return value.toDouble();
    return double.tryParse(value?.toString() ?? '');
  }
}