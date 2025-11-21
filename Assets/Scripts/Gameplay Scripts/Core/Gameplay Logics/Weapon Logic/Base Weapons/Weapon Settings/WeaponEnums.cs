public enum WeaponType
{
    Basic = 0,
    Shuriken = 1,
    Kunai = 2,
}

public enum WeaponPatternType
{
    StraightLine,          // basic horizontal row (with overflow rules)
    AlternatingBurst,      // L-R-L-R missiles
    FanSequential          // kunai: center, +step, -step, ...
}

public enum ProjectileMotionType
{
    Linear,
    Sine
}

public enum WeaponUpgradeType
{
    ProjectileAmount,      // +N projectiles per trigger
    FireRate,              // +% to fire rate (per second)
    Piercing               // +N pierces
}

public enum OverflowResolution
{
    Rows,                  // prefer adding rows when amount > limit
    RapidStreak            // prefer very fast overflow shots
}

public enum WeaponUpgradeScope
{
    SpecificWeapon,
    AllWeapons
}

public enum WeaponUpgradeRarity
{
    Common = 0,
    Rare = 1,
    Epic = 2,
    Legendary = 3
}

public enum HitCountPolicy
{
    /// <summary>Count a hit only once per target for the life of a single projectile.</summary>
    OncePerTargetPerProjectile,

    /// <summary>Count every distinct OnTriggerEnter (re-hits are allowed, e.g., boomerang).</summary>
    CountEveryEntry
}
