import 'package:flutter/material.dart';
import '../models/item.dart';

class ItemCard extends StatelessWidget {
  final Item item;
  final VoidCallback onTap;

  const ItemCard({super.key, required this.item, required this.onTap});

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(8),
      child: Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: const Color(0xFF0D1320),
          borderRadius: BorderRadius.circular(8),
          border: Border.all(color: const Color(0xFF1F2937)),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _MarketIcon(item: item),
                const SizedBox(width: 10),
                Expanded(
                  child: Text(
                    item.title,
                    maxLines: 3,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 15,
                      fontWeight: FontWeight.w700,
                      height: 1.25,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            Container(
              padding: const EdgeInsets.symmetric(vertical: 4),
              decoration: BoxDecoration(
                color: const Color(0xFF0B1018),
                borderRadius: BorderRadius.circular(8),
              ),
              child: Column(
                children: [
                  _PriceLine(
                    label: 'Yes',
                    price: item.priceYes,
                    color: const Color(0xFF00A3FF),
                  ),
                  const Divider(
                    height: 1,
                    color: Color(0xFF1F2937),
                  ),
                  _PriceLine(
                    label: 'No',
                    price: item.priceNo,
                    color: const Color(0xFFEF4444),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  // _formatMoney removed (not used)
}

class _MarketIcon extends StatelessWidget {
  final Item item;

  const _MarketIcon({required this.item});

  @override
  Widget build(BuildContext context) {
    final colors = [
      const Color(0xFF00A3FF),
      const Color(0xFF22C55E),
      const Color(0xFFF59E0B),
      const Color(0xFF8B5CF6),
      const Color(0xFFEF4444),
    ];
    final color = colors[
        item.id.codeUnits.fold(0, (sum, unit) => sum + unit) % colors.length];

    return Container(
      width: 42,
      height: 42,
      decoration: BoxDecoration(
        color: color.withOpacity(0.14),
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: color.withOpacity(0.45)),
      ),
      child: Icon(Icons.insights, color: color, size: 22),
    );
  }
}

class _PriceLine extends StatelessWidget {
  final String label;
  final double price;
  final Color color;

  const _PriceLine({
    required this.label,
    required this.price,
    required this.color,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      child: Row(
        children: [
          SizedBox(
            width: 36,
            child: Text(
              label,
              style: TextStyle(
                color: color,
                fontSize: 12,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
          Expanded(
            child: Text(
              '${(price * 100).toStringAsFixed(0)}¢',
              textAlign: TextAlign.right,
              style: const TextStyle(
                color: Colors.white,
                fontSize: 15,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
