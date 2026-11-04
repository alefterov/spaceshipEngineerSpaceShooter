using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum DifficultyTier { Easy, Medium, Hard }

public static class LevelDifficulty
{
    public struct Result
    {
        public float ratio;
        public DifficultyTier tier;
        public float enemyPower;
        public float playerPower;
        public int enemyShips;
    }

    public static Result Calculate(LevelDefinition level, IEnumerable<ShipModule> playerBlocks, float mediumFrom, float hardFrom)
    {
        var result = new Result { playerPower = Power(playerBlocks) };

        float total = 0f;
        if (level != null && level.waves != null)
        {
            foreach (var wave in level.waves)
            {
                if (wave == null) continue;

                foreach (var entry in wave.enemies)
                {
                    if (entry == null || entry.enemyPrefab == null || !entry.enemyPrefab.TryGetComponent<EnemyShip>(out var ship)) continue;

                    float power = Power(ship.blocks.Select(p => p.block != null && p.block.prefab != null
                        ? p.block.prefab.GetComponent<ShipModule>() : null));

                    int count = Mathf.Max(1, entry.count);
                    total += power * count;
                    result.enemyShips += count;
                }
            }
        }

        result.enemyPower = result.enemyShips > 0 ? total / result.enemyShips : 0f;

        if (result.enemyPower <= 0f) result.ratio = 0f;
        else if (result.playerPower <= 0f) result.ratio = float.PositiveInfinity;
        else result.ratio = result.enemyPower / result.playerPower;

        result.tier = result.ratio >= hardFrom ? DifficultyTier.Hard
                    : result.ratio >= mediumFrom ? DifficultyTier.Medium
                    : DifficultyTier.Easy;
        return result;
    }

    private static float Power(IEnumerable<ShipModule> blocks)
    {
        if (blocks == null) return 0f;

        var list = blocks.Where(b => b != null).ToList();
        if (list.Count == 0) return 0f;

        float averageHp = list.Average(b => b.maxHP);
        float damage = list.OfType<WeaponModule>().Sum(w => w.NominalDamagePerSecond);
        return averageHp * damage;
    }
}
