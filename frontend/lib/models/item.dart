class Item {
  final String id;
  final String title;
  final String ownerId;
  final int priceYes;
  final int priceNo;
  final int potSize;

  const Item({
    required this.id,
    required this.title,
    required this.ownerId,
    required this.priceYes,
    required this.priceNo,
    required this.potSize,
  });

  factory Item.fromJson(Map<String, dynamic> json) {
    final rawId = json['Id'] ?? json['id'];
    final rawTitle = json['Title'] ?? json['title'];
    final rawOwnerId = json['OwnerId'] ?? json['ownerId'];
    final rawPriceYes = json['PriceYes'] ?? json['priceYes'];
    final rawPriceNo = json['PriceNo'] ?? json['priceNo'];
    final rawPotSize = json['PotSize'] ?? json['potSize'];

    return Item(
      id: rawId?.toString() ?? '',
      title: rawTitle?.toString() ?? 'Untitled event',
      ownerId: rawOwnerId?.toString() ?? '',
      priceYes: _toInt(rawPriceYes) ?? 0,
      priceNo: _toInt(rawPriceNo) ?? 0,
      potSize: _toInt(rawPotSize) ?? 0,
    );
  }

  String get category {
    final categories = ['Politics', 'Crypto', 'Sports', 'Culture', 'Macro'];
    return categories[_stableNumber % categories.length];
  }

  String get closeLabel => '${3 + _stableNumber % 26} days left';

  String get changeLabel {
    final value = (_stableNumber * 3 % 17) - 8;
    if (value == 0) return 'flat';
    return '${value > 0 ? '+' : ''}$value%';
  }

  bool get isPositiveChange => !changeLabel.startsWith('-');

  int get liquidity => 12000 + (_stableNumber * 919 % 88000);

  int get _stableNumber => id.codeUnits.fold(0, (sum, unit) => sum + unit);

  static int? _toInt(Object? value) {
    if (value is int) return value;
    if (value is num) return value.toInt();
    return int.tryParse(value?.toString() ?? '');
  }
}
