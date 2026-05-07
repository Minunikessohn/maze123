#nullable enable

using System.Collections.Generic;
using Godot;
using Maze.Game.Settings;
using Maze.Views;

namespace Maze.Audio;

public partial class HorrorAudioController : Node
{
	private const string AudioRootPath = "res://assets/audio/horror/";
	private const float SilentDb = -80f;
	private const float MaxMonsterAudibleDistanceCells = 8f;
	private const float MonsterDangerDistanceCells = 3.5f;
	private const float LowStaminaThreshold = 0.35f;
	private const float FootstepWalkIntervalSeconds = 0.48f;
	private const float FootstepSprintIntervalSeconds = 0.3f;
	private const float SprintFootstepVolumeBoost = 1.45f;
	private const float SprintMonsterCueBias = 0.12f;
	private const float MonsterCueMinIntervalSeconds = 0.6f;
	private const float MonsterCueMaxIntervalSeconds = 2.3f;
	private const float HeartbeatMinIntervalSeconds = 0.52f;
	private const float HeartbeatMaxIntervalSeconds = 1.2f;
	private const float ExhaustionMinIntervalSeconds = 0.9f;
	private const float ExhaustionMaxIntervalSeconds = 2.1f;

	private static readonly string[] FootstepFiles =
	{
		"data_pion-st1-footstep-sfx-323053.mp3",
		"data_pion-st2-footstep-sfx-323055.mp3",
		"data_pion-st3-footstep-sfx-323056.mp3"
	};

	private static readonly string[] MonsterCueFiles =
	{
		"dragon-studio-ghost-whisper-351569.mp3",
		"freesound_community-whisper-trail-1-105420.mp3",
		"chiiri-monster-15-337349.mp3"
	};

	private readonly List<AudioStream> _footstepStreams = new();
	private readonly List<AudioStream> _monsterCueStreams = new();
	private readonly List<Vector2I> _monsterCells = new();
	private readonly RandomNumberGenerator _random = new();

	private AudioStreamPlayer _footstepPlayer = null!;
	private AudioStreamPlayer _monsterCuePlayer = null!;
	private AudioStreamPlayer _heartbeatPlayer = null!;
	private AudioStreamPlayer _exhaustionPlayer = null!;
	private AudioStream _heartbeatStream = null!;
	private AudioStream _exhaustionStream = null!;
	private AudioSettings _settings = new();
	private PlayerCharacter3D? _player;
	private Vector2I? _playerCell;
	private float _currentStamina = 1f;
	private float _maximumStamina = 1f;
	private bool _isSprinting;
	private bool _gameplayActive;
	private bool _manualModeActive;
	private float _footstepCooldownRemaining;
	private float _monsterCueCooldownRemaining;
	private float _heartbeatCooldownRemaining;
	private float _exhaustionCooldownRemaining;

	public override void _Ready()
	{
		_footstepPlayer = CreatePlayer("Footsteps");
		_monsterCuePlayer = CreatePlayer("MonsterCue");
		_heartbeatPlayer = CreatePlayer("Heartbeat");
		_exhaustionPlayer = CreatePlayer("Exhaustion");

		foreach (string file in FootstepFiles)
		{
			_footstepStreams.Add(LoadMp3Stream(AudioRootPath + file));
		}

		foreach (string file in MonsterCueFiles)
		{
			_monsterCueStreams.Add(LoadMp3Stream(AudioRootPath + file));
		}

		_heartbeatStream = LoadMp3Stream(AudioRootPath + "soundreality-heart-beat-137135.mp3");
		_exhaustionStream = LoadMp3Stream(AudioRootPath + "freesound_community-hear-race-and-give-out-78043.mp3");
		ApplyPlayerVolumes();
	}

	public override void _Process(double delta)
	{
		if (!_gameplayActive)
		{
			StopAll();
			return;
		}

		float deltaSeconds = (float)delta;
		UpdateFootsteps(deltaSeconds);
		UpdateMonsterCue(deltaSeconds);
		UpdateHeartbeat(deltaSeconds);
		UpdateExhaustion(deltaSeconds);
	}

	public void BindPlayer(PlayerCharacter3D player)
	{
		_player = player;
	}

	public void SetAudioSettings(AudioSettings settings)
	{
		_settings.MonsterVolume = Mathf.Clamp(settings.MonsterVolume, 0f, 1f);
		_settings.FootstepVolume = Mathf.Clamp(settings.FootstepVolume, 0f, 1f);
		_settings.GoalVolume = Mathf.Clamp(settings.GoalVolume, 0f, 1f);
		_settings.MasterVolume = Mathf.Clamp(settings.MasterVolume, 0f, 1f);
		ApplyPlayerVolumes();
	}

	public void SetGameplayState(bool gameplayActive, bool manualModeActive)
	{
		_gameplayActive = gameplayActive;
		_manualModeActive = manualModeActive;

		if (!_gameplayActive)
		{
			StopAll();
		}
	}

	public void UpdatePlayerCell(Vector2I? playerCell)
	{
		_playerCell = playerCell;
	}

	public void SetMonsterCells(IEnumerable<Vector2I> monsterCells)
	{
		_monsterCells.Clear();

		foreach (Vector2I cell in monsterCells)
		{
			_monsterCells.Add(cell);
		}
	}

	public void SetPlayerStamina(float current, float maximum, bool sprinting)
	{
		_currentStamina = Mathf.Max(0f, current);
		_maximumStamina = Mathf.Max(0.001f, maximum);
		_isSprinting = sprinting;
	}

	private AudioStreamPlayer CreatePlayer(string name)
	{
		AudioStreamPlayer player = new()
		{
			Name = name,
			Bus = "Master",
			ProcessMode = ProcessModeEnum.Always
		};

		AddChild(player);
		return player;
	}

	private void UpdateFootsteps(float deltaSeconds)
	{
		_footstepCooldownRemaining = Mathf.Max(0f, _footstepCooldownRemaining - deltaSeconds);

		if (!_manualModeActive
			|| _player is null
			|| _player.CurrentMode != PlayerCharacter3D.Mode.Manual
			|| !_player.IsMoving
			|| _settings.MasterVolume <= 0f
			|| _settings.FootstepVolume <= 0f)
		{
			return;
		}

		if (_footstepCooldownRemaining > 0f)
		{
			return;
		}

		_footstepPlayer.Stream = PickRandom(_footstepStreams);
		_footstepPlayer.PitchScale = _isSprinting
			? _random.RandfRange(1.04f, 1.14f)
			: _random.RandfRange(0.94f, 1.06f);
		float footstepVolume = _settings.MasterVolume * _settings.FootstepVolume * (_isSprinting ? 0.55f * SprintFootstepVolumeBoost : 0.48f);
		_footstepPlayer.VolumeDb = ToDecibels(footstepVolume);
		_footstepPlayer.Play();
		_footstepCooldownRemaining = _isSprinting ? FootstepSprintIntervalSeconds : FootstepWalkIntervalSeconds;
	}

	private void UpdateMonsterCue(float deltaSeconds)
	{
		_monsterCueCooldownRemaining = Mathf.Max(0f, _monsterCueCooldownRemaining - deltaSeconds);
		float monsterIntensity = ComputeMonsterIntensity();

		if (monsterIntensity <= 0f || _settings.MasterVolume <= 0f || _settings.MonsterVolume <= 0f)
		{
			return;
		}

		if (_monsterCuePlayer.Playing || _monsterCueCooldownRemaining > 0f)
		{
			return;
		}

		_monsterCuePlayer.Stream = PickRandom(_monsterCueStreams);
		_monsterCuePlayer.PitchScale = _random.RandfRange(0.92f, 1.06f);
		_monsterCuePlayer.VolumeDb = ToDecibels(_settings.MasterVolume * _settings.MonsterVolume * Mathf.Lerp(0.18f, 0.82f, monsterIntensity));
		_monsterCuePlayer.Play();
		_monsterCueCooldownRemaining = Mathf.Lerp(MonsterCueMaxIntervalSeconds, MonsterCueMinIntervalSeconds, monsterIntensity);
	}

	private void UpdateHeartbeat(float deltaSeconds)
	{
		_heartbeatCooldownRemaining = Mathf.Max(0f, _heartbeatCooldownRemaining - deltaSeconds);
		float heartbeatIntensity = Mathf.Max(ComputeMonsterIntensity() * 0.9f, ComputeLowStaminaIntensity());

		if (heartbeatIntensity <= 0f || _settings.MasterVolume <= 0f || _settings.GoalVolume <= 0f)
		{
			return;
		}

		if (_heartbeatPlayer.Playing || _heartbeatCooldownRemaining > 0f)
		{
			return;
		}

		_heartbeatPlayer.Stream = _heartbeatStream;
		_heartbeatPlayer.PitchScale = Mathf.Lerp(0.92f, 1.18f, heartbeatIntensity);
		_heartbeatPlayer.VolumeDb = ToDecibels(_settings.MasterVolume * _settings.GoalVolume * Mathf.Lerp(0.14f, 0.72f, heartbeatIntensity));
		_heartbeatPlayer.Play();
		_heartbeatCooldownRemaining = Mathf.Lerp(HeartbeatMaxIntervalSeconds, HeartbeatMinIntervalSeconds, heartbeatIntensity);
	}

	private void UpdateExhaustion(float deltaSeconds)
	{
		_exhaustionCooldownRemaining = Mathf.Max(0f, _exhaustionCooldownRemaining - deltaSeconds);
		float exhaustionIntensity = ComputeLowStaminaIntensity();

		if (!_manualModeActive
			|| exhaustionIntensity <= 0.08f
			|| _settings.MasterVolume <= 0f
			|| _settings.GoalVolume <= 0f)
		{
			return;
		}

		if (_exhaustionPlayer.Playing || _exhaustionCooldownRemaining > 0f)
		{
			return;
		}

		_exhaustionPlayer.Stream = _exhaustionStream;
		_exhaustionPlayer.PitchScale = Mathf.Lerp(0.96f, 1.08f, exhaustionIntensity);
		_exhaustionPlayer.VolumeDb = ToDecibels(_settings.MasterVolume * _settings.GoalVolume * Mathf.Lerp(0.12f, 0.74f, exhaustionIntensity));
		_exhaustionPlayer.Play();
		_exhaustionCooldownRemaining = Mathf.Lerp(ExhaustionMaxIntervalSeconds, ExhaustionMinIntervalSeconds, exhaustionIntensity);
	}

	private void ApplyPlayerVolumes()
	{
		_footstepPlayer.VolumeDb = ToDecibels(_settings.MasterVolume * _settings.FootstepVolume * 0.55f);
		_monsterCuePlayer.VolumeDb = ToDecibels(_settings.MasterVolume * _settings.MonsterVolume * 0.45f);
		_heartbeatPlayer.VolumeDb = ToDecibels(_settings.MasterVolume * _settings.GoalVolume * 0.4f);
		_exhaustionPlayer.VolumeDb = ToDecibels(_settings.MasterVolume * _settings.GoalVolume * 0.4f);
	}

	private void StopAll()
	{
		_footstepCooldownRemaining = 0f;
		_monsterCueCooldownRemaining = 0f;
		_heartbeatCooldownRemaining = 0f;
		_exhaustionCooldownRemaining = 0f;
		_footstepPlayer.Stop();
		_monsterCuePlayer.Stop();
		_heartbeatPlayer.Stop();
		_exhaustionPlayer.Stop();
	}

	private float ComputeLowStaminaIntensity()
	{
		if (!_manualModeActive)
		{
			return 0f;
		}

		float staminaRatio = Mathf.Clamp(_currentStamina / _maximumStamina, 0f, 1f);
		if (staminaRatio >= LowStaminaThreshold)
		{
			return 0f;
		}

		return 1f - staminaRatio / LowStaminaThreshold;
	}

	private float ComputeMonsterIntensity()
	{
		if (_playerCell is not Vector2I playerCell || _monsterCells.Count == 0)
		{
			return 0f;
		}

		float nearestDistance = float.MaxValue;
		foreach (Vector2I monsterCell in _monsterCells)
		{
			float distance = Mathf.Abs(monsterCell.X - playerCell.X) + Mathf.Abs(monsterCell.Y - playerCell.Y);
			if (distance < nearestDistance)
			{
				nearestDistance = distance;
			}
		}

		if (nearestDistance > MaxMonsterAudibleDistanceCells)
		{
			return 0f;
		}

		float distanceFactor = 1f - nearestDistance / MaxMonsterAudibleDistanceCells;
		float dangerBoost = nearestDistance <= MonsterDangerDistanceCells
			? 0.25f * (1f - nearestDistance / MonsterDangerDistanceCells)
			: 0f;
		float sprintBias = _isSprinting ? SprintMonsterCueBias : 0f;
		return Mathf.Clamp(distanceFactor + dangerBoost + sprintBias, 0f, 1f);
	}

	private AudioStream PickRandom(IReadOnlyList<AudioStream> streams)
	{
		int index = _random.RandiRange(0, streams.Count - 1);
		return streams[index];
	}

	private static AudioStream LoadMp3Stream(string path)
	{
		byte[] data = FileAccess.GetFileAsBytes(path);
		if (data.Length == 0)
		{
			GD.PushError($"[HorrorAudioController] Audiodatei konnte nicht gelesen werden: {path}");
			return new AudioStreamMP3();
		}

		return new AudioStreamMP3
		{
			Data = data
		};
	}

	private static float ToDecibels(float linearVolume)
	{
		float clampedVolume = Mathf.Clamp(linearVolume, 0f, 1f);
		return clampedVolume <= 0.0001f ? SilentDb : Mathf.LinearToDb(clampedVolume);
	}
}
