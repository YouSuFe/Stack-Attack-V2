#  Stack-Attack Style Prototype (Unity 2D)

This Unity project is a **modular hyper-casual shooter prototype** inspired by *Stack Attack* / *Vampire Survivors* vibes.  
It emphasizes clean, extensible architecture using **ScriptableObjects**, **interfaces**, and **modular weapon logic** — now with **Unity’s built-in object pooling** for high-perf projectiles.

---

##  Core Features

###  Player Movement
- **Touch/Mouse drag** movement via the **New Input System**.
- `PlayerDragMover` uses `Rigidbody2D (Kinematic, Interpolate)` for smooth horizontal sliding.
- All input is centralized in `InputReader` (ScriptableObject) + `InputReaderController` (Mono) to keep gameplay decoupled from input actions.

###  Weapon System (Data-Driven)
- Each weapon is defined by a **WeaponDefinitionSO** and collected in a **WeaponCatalog**.
- `WeaponDriver` equips weapons at runtime, handles **fire input** (tap = fire once, hold = auto; release = stop), and applies **upgrades** (fire rate %, amount +, piercing +).
- Implemented weapons:
  1. **BasicWeapon** – Straight-line pattern with row balancing (odd/even centering), overflow rows, spacing & per-row forward offset.
  2. **MissileWeapon** – Alternating left/right muzzles; sine/cosine arcs; burst schedules respecting fire-rate windows.
  3. **KunaiWeapon** – “Fan sequential” shots with adjustable step degrees and max spread.

###  Projectiles, Damage & Hit Counting
- `ProjectileBase` (inherits `PooledProjectile`) implements:
  - **IDamageDealer** (exposes `DamageAmount`, `Owner`)
  - **IProjectile** (`Initialize(owner, damage, pierce, policy)`)
- Built-in **lifetime**, **piercing**, **hit counting**, and **pool return** (no `Destroy`, uses `Despawn()`).
- **HitCountPolicy** per projectile:
  - `OncePerTargetPerProjectile` (default bullets/kunai)
  - `CountEveryEntry` (for boomerangs/ricochets)
- On valid hit:
  - `IDamageable.TakeDamage(...)` is called
  - **HitEventBus** raises a “player hit” event to charge specials

### Special Skill System (Laser)
- **Hit-based charge** (not damage-based). `SpecialSkillDriver` listens to `HitEventBus`.
- **No cooldown**. Fires **only on input release** and **only in combat** when bar is full; empties bar on fire.
- `LaserSkill` modes:
  - `SingleImpact` – one-time damage on activation (visuals persist for duration)
  - `Continuous` – damage **every N seconds** (`TickIntervalSeconds`, default 1.0s)
- Laser hits can **refill the bar**; optional per-activation “count once per enemy” for balanced pacing.

###  Health & Invulnerability
- `PlayerHealth` supports hearts, invulnerability window, shield visual toggle + blink at tail end.
- Implements `IDamageable` to receive projectile & enemy contact damage.

###  Enemy (Test)
- `TestEnemy` implements **IDamageable + IDamageDealer**.
- Moves toward player; deals contact damage with per-target cooldown; logs damage & death.

---

##  High-Performance Pooling

### Why built-in pooling?
Uses `UnityEngine.Pool.ObjectPool<T>` for zero-GC, typed pools with lifecycle hooks.

### Components
- **`PooledProjectile`**: base class giving each projectile a `pool` reference and `Despawn()` method.
- **`ProjectileBase : PooledProjectile`**: replaces all `Destroy()` calls with `Despawn()`, resets state in `OnFetchedFromPool()`.
- **`ProjectilePoolHub`**: owns **one pool per projectile prefab**, supports **prewarm**, **capacity** and hierarchy parenting.
- **Weapons** request instances via the hub instead of `Instantiate()`.
