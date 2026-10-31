using System.Text.RegularExpressions;
using PlaySense;
using PlaySense.Attention;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;

/// <summary>
/// Connects Unity's FPS Microgame to PlaySense without editing any Microgame code.
///
/// Put it on the PlaySenseManager object in MainScene. It:
///  - forwards Microgame events (kills, pickups, deaths, objectives, win/lose) to PlaySense,
///  - logs shots, weapon switches and damage taken, with the weapon/source as metadata,
///  - tags every enemy and pickup as an attention target, so you see what players noticed.
///
/// Then check Dashboard > All Sessions > Events for questions like
/// "which weapon got the kills?" and "what killed players most?".
/// </summary>
public class PlaySenseFPSMicrogameBridge : MonoBehaviour
{
    [Tooltip("Tag every enemy as a PlaySense attention target (category Enemy).")]
    [SerializeField] private bool markEnemiesAsTargets = true;

    [Tooltip("Tag every pickup as a PlaySense attention target (category Pickup).")]
    [SerializeField] private bool markPickupsAsTargets = true;

    [Tooltip("Log one event per shot. Turn off for long sessions with automatic weapons.")]
    [SerializeField] private bool logEveryShot = true;

    private PlayerWeaponsManager _weapons;
    private PlayerCharacterController _player;
    private Health _playerHealth;
    private WeaponController _hookedWeapon;
    private string _lastDamageSource = "unknown";

    private static readonly Regex CloneSuffix = new Regex(@"(\s*\(Clone\))|(\s*\(\d+\))$");

    // =====================================================
    // LIFECYCLE
    // =====================================================

    private void OnEnable()
    {
        EventManager.AddListener<EnemyKillEvent>(OnEnemyKilled);
        EventManager.AddListener<PickupEvent>(OnPickup);
        EventManager.AddListener<AmmoPickupEvent>(OnAmmoPickup);
        EventManager.AddListener<PlayerDeathEvent>(OnPlayerDeath);
        EventManager.AddListener<GameOverEvent>(OnGameOver);
        EventManager.AddListener<ObjectiveUpdateEvent>(OnObjectiveUpdate);
        EventManager.AddListener<AllObjectivesCompletedEvent>(OnAllObjectivesCompleted);
    }

    private void OnDisable()
    {
        EventManager.RemoveListener<EnemyKillEvent>(OnEnemyKilled);
        EventManager.RemoveListener<PickupEvent>(OnPickup);
        EventManager.RemoveListener<AmmoPickupEvent>(OnAmmoPickup);
        EventManager.RemoveListener<PlayerDeathEvent>(OnPlayerDeath);
        EventManager.RemoveListener<GameOverEvent>(OnGameOver);
        EventManager.RemoveListener<ObjectiveUpdateEvent>(OnObjectiveUpdate);
        EventManager.RemoveListener<AllObjectivesCompletedEvent>(OnAllObjectivesCompleted);

        UnhookPlayer();
    }

    private void Start()
    {
        HookPlayer();

        if (markEnemiesAsTargets)
        {
            foreach (EnemyController enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                MarkTarget(enemy.gameObject, "Enemy");
        }

        if (markPickupsAsTargets)
        {
            foreach (Pickup pickup in FindObjectsByType<Pickup>(FindObjectsSortMode.None))
                MarkTarget(pickup.gameObject, "Pickup");
        }
    }

    // =====================================================
    // PLAYER, WEAPONS, DAMAGE
    // If your Microgame version names any of these differently,
    // Unity will point at a line here: delete that part and the
    // rest of the bridge still works.
    // =====================================================

    private void HookPlayer()
    {
        _player = FindFirstObjectByType<PlayerCharacterController>();
        if (_player == null)
        {
            Debug.LogWarning("[PlaySense] FPS bridge: no PlayerCharacterController found. Is this the Microgame's MainScene?");
            return;
        }

        _playerHealth = _player.GetComponent<Health>();
        if (_playerHealth != null)
            _playerHealth.OnDamaged += OnPlayerDamaged;

        _weapons = _player.GetComponent<PlayerWeaponsManager>();
        if (_weapons != null)
        {
            _weapons.OnSwitchedToWeapon += OnSwitchedWeapon;
            HookWeapon(_weapons.GetActiveWeapon());
        }
    }

    private void UnhookPlayer()
    {
        if (_playerHealth != null)
            _playerHealth.OnDamaged -= OnPlayerDamaged;

        if (_weapons != null)
            _weapons.OnSwitchedToWeapon -= OnSwitchedWeapon;

        HookWeapon(null);
    }

    private void HookWeapon(WeaponController weapon)
    {
        if (_hookedWeapon != null)
            _hookedWeapon.OnShoot -= OnShoot;

        _hookedWeapon = weapon;

        if (_hookedWeapon != null)
            _hookedWeapon.OnShoot += OnShoot;
    }

    private void OnSwitchedWeapon(WeaponController weapon)
    {
        HookWeapon(weapon);

        if (weapon != null)
            PlaySenseEvents.LogWithData("Weapon", "Switched", weapon.WeaponName, "weapon", weapon.WeaponName);
    }

    private void OnShoot()
    {
        if (!logEveryShot || _player == null)
            return;

        PlaySenseEvents.Log("Combat", "Shot", _player.gameObject, "weapon", CurrentWeapon());
    }

    private void OnPlayerDamaged(float damage, GameObject damageSource)
    {
        _lastDamageSource = damageSource != null ? Clean(damageSource.name) : "environment";

        PlaySenseEvents.Log("Player", "Damaged", _player != null ? _player.gameObject : null,
            "source", _lastDamageSource,
            "amount", Mathf.RoundToInt(damage).ToString());
    }

    // =====================================================
    // MICROGAME EVENTS
    // =====================================================

    private void OnEnemyKilled(EnemyKillEvent evt)
    {
        if (evt.Enemy == null)
            return;

        PlaySenseEvents.Log("Combat", "Kill", evt.Enemy,
            "weapon", CurrentWeapon(),
            "enemy", Clean(evt.Enemy.name),
            "remaining", evt.RemainingEnemyCount.ToString());
    }

    private void OnPickup(PickupEvent evt)
    {
        if (evt.Pickup != null)
            PlaySenseEvents.Log("Pickup", "Collected", evt.Pickup, "type", Clean(evt.Pickup.name));
    }

    private void OnAmmoPickup(AmmoPickupEvent evt)
    {
        string weapon = evt.Weapon != null ? evt.Weapon.WeaponName : "unknown";
        PlaySenseEvents.LogWithData("Pickup", "Ammo", weapon, "weapon", weapon);
    }

    private void OnPlayerDeath(PlayerDeathEvent evt)
    {
        PlaySenseEvents.Log("Player", "Died", _player != null ? _player.gameObject : null,
            "killedBy", _lastDamageSource,
            "weapon", CurrentWeapon());
    }

    private void OnGameOver(GameOverEvent evt)
    {
        PlaySenseEvents.LogWithData("Game", "Over", null, "result", evt.Win ? "win" : "lose");
    }

    private void OnObjectiveUpdate(ObjectiveUpdateEvent evt)
    {
        if (evt.IsComplete)
            PlaySenseEvents.LogWithData("Objective", "Completed", evt.DescriptionText, "objective", evt.DescriptionText);
    }

    private void OnAllObjectivesCompleted(AllObjectivesCompletedEvent evt)
    {
        PlaySenseEvents.Log("Objective", "AllCompleted");
    }

    // =====================================================
    // HELPERS
    // =====================================================

    private string CurrentWeapon()
    {
        if (_weapons == null)
            return "none";

        WeaponController weapon = _weapons.GetActiveWeapon();
        return weapon != null ? weapon.WeaponName : "none";
    }

    private static void MarkTarget(GameObject target, string category)
    {
        if (target.GetComponent<PlaySenseTarget>() == null)
            target.AddComponent<PlaySenseTarget>().Configure(Clean(target.name), category);
    }

    // "Enemy_HoverBot (3)" and "Pickup_Health(Clone)" group as one name in reports.
    private static string Clean(string objectName)
    {
        return CloneSuffix.Replace(objectName ?? string.Empty, string.Empty).Trim();
    }
}
