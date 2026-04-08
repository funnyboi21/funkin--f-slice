class_name Scoring

## Which system to use when scoring and judging notes.
enum ScoringSystem {
	LEGACY,  ## Week 6 and older. Step function judgement.
	WEEK7,   ## Week 7. Tighter windows than Legacy.
	PBOT1,   ## Points Based On Timing v1. Sigmoid function.
}

## Rank awarded at the end of a song.
enum ScoringRank {
	PERFECT_GOLD,
	PERFECT,
	EXCELLENT,
	GREAT,
	GOOD,
	SHIT,
}

# ─── PBOT1 Constants ──────────────────────────────────────────────────────────
const PBOT1_MAX_SCORE        : int   = 500
const PBOT1_SCORING_OFFSET   : float = 54.99
const PBOT1_SCORING_SLOPE    : float = 0.080
const PBOT1_MIN_SCORE        : float = 9.0
const PBOT1_MISS_SCORE       : int   = -100
const PBOT1_PERFECT_THRESHOLD: float = 5.0    # 5 ms
const PBOT1_MISS_THRESHOLD   : float = 160.0  # 160 ms
const PBOT1_KILLER_THRESHOLD : float = 12.5   # ~7.5% of hit window
const PBOT1_SICK_THRESHOLD   : float = 45.0   # ~25% of hit window
const PBOT1_GOOD_THRESHOLD   : float = 90.0   # ~55% of hit window
const PBOT1_BAD_THRESHOLD    : float = 135.0  # ~85% of hit window
const PBOT1_SHIT_THRESHOLD   : float = 160.0  # 100% of hit window

# ─── Legacy Constants ─────────────────────────────────────────────────────────
const LEGACY_HIT_WINDOW    : float = (10.0 / 60.0) * 1000.0  # ~166.67 ms
const LEGACY_SICK_THRESHOLD: float = 0.2
const LEGACY_GOOD_THRESHOLD: float = 0.75
const LEGACY_BAD_THRESHOLD : float = 0.9
const LEGACY_SHIT_THRESHOLD: float = 1.0
const LEGACY_SICK_SCORE    : int   = 350
const LEGACY_GOOD_SCORE    : int   = 200
const LEGACY_BAD_SCORE     : int   = 100
const LEGACY_SHIT_SCORE    : int   = 50
const LEGACY_MISS_SCORE    : int   = -10

# ─── Week 7 Constants ─────────────────────────────────────────────────────────
const WEEK7_HIT_WINDOW    : float = (10.0 / 60.0) * 1000.0  # same as Legacy
const WEEK7_SICK_THRESHOLD: float = 0.2
const WEEK7_GOOD_THRESHOLD: float = 0.55
const WEEK7_BAD_THRESHOLD : float = 0.8
const WEEK7_SICK_SCORE    : int   = 350
const WEEK7_GOOD_SCORE    : int   = 200
const WEEK7_BAD_SCORE     : int   = 100
const WEEK7_SHIT_SCORE    : int   = 50
const WEEK7_MISS_SCORE    : int   = -10

# ─── Rank Thresholds (mirror Constants.hx values) ─────────────────────────────
const RANK_PERFECT_THRESHOLD  : float = 1.0
const RANK_EXCELLENT_THRESHOLD: float = 0.9
const RANK_GREAT_THRESHOLD    : float = 0.7
const RANK_GOOD_THRESHOLD     : float = 0.5

# ==============================================================================
# Public API
# ==============================================================================

## Returns the integer score for a note hit.
## [param ms_timing] Signed difference between note time and hit time (ms).
static func score_note(ms_timing: float, system: ScoringSystem = ScoringSystem.PBOT1) -> int:
	match system:
		ScoringSystem.LEGACY: return _score_note_legacy(ms_timing)
		ScoringSystem.WEEK7:  return _score_note_week7(ms_timing)
		ScoringSystem.PBOT1:  return _score_note_pbot1(ms_timing)
		_:
			push_error("Unknown scoring system: %s" % system)
			return 0

## Returns the judgement string for a note hit ("sick", "good", "bad", "shit", "miss").
static func judge_note(ms_timing: float, system: ScoringSystem = ScoringSystem.PBOT1) -> String:
	match system:
		ScoringSystem.LEGACY: return _judge_note_legacy(ms_timing)
		ScoringSystem.WEEK7:  return _judge_note_week7(ms_timing)
		ScoringSystem.PBOT1:  return _judge_note_pbot1(ms_timing)
		_:
			push_error("Unknown scoring system: %s" % system)
			return "miss"

## Returns the score penalty applied when a note is missed.
static func get_miss_score(system: ScoringSystem = ScoringSystem.PBOT1) -> int:
	match system:
		ScoringSystem.LEGACY: return LEGACY_MISS_SCORE
		ScoringSystem.WEEK7:  return WEEK7_MISS_SCORE
		ScoringSystem.PBOT1:  return PBOT1_MISS_SCORE
		_:
			push_error("Unknown scoring system: %s" % system)
			return 0

## Calculates the rank from a score tally dictionary.
## Expected keys: sick, good, bad, shit, missed, totalNotes
static func calculate_rank(tally: Dictionary) -> ScoringRank:
	if tally.is_empty() or tally.get("totalNotes", 0) == 0:
		return ScoringRank.SHIT

	if tally["sick"] == tally["totalNotes"]:
		return ScoringRank.PERFECT_GOLD

	var completion := tally_completion(tally)

	if completion >= RANK_PERFECT_THRESHOLD:
		return ScoringRank.PERFECT
	elif completion >= RANK_EXCELLENT_THRESHOLD:
		return ScoringRank.EXCELLENT
	elif completion >= RANK_GREAT_THRESHOLD:
		return ScoringRank.GREAT
	elif completion >= RANK_GOOD_THRESHOLD:
		return ScoringRank.GOOD
	else:
		return ScoringRank.SHIT

## Returns a 0–1 completion value: (sick + good − missed) / totalNotes.
static func tally_completion(tally: Dictionary) -> float:
	if tally.is_empty():
		return 0.0
	var raw := float(tally.get("sick", 0) + tally.get("good", 0) - tally.get("missed", 0)) \
		/ float(tally.get("totalNotes", 1))
	return clampf(raw, 0.0, 1.0)

## Returns a numeric value for rank comparison (higher = better).
static func rank_value(rank: ScoringRank) -> int:
	match rank:
		ScoringRank.PERFECT_GOLD: return 5
		ScoringRank.PERFECT:      return 4
		ScoringRank.EXCELLENT:    return 3
		ScoringRank.GREAT:        return 2
		ScoringRank.GOOD:         return 1
		ScoringRank.SHIT:         return 0
		_:                        return -1

# ==============================================================================
# PBOT1 internals
# ==============================================================================

static func _score_note_pbot1(ms_timing: float) -> int:
	var t := absf(ms_timing)
	if t > PBOT1_MISS_THRESHOLD:
		return PBOT1_MISS_SCORE
	if t < PBOT1_PERFECT_THRESHOLD:
		return PBOT1_MAX_SCORE
	# Sigmoid curve
	var factor := 1.0 - (1.0 / (1.0 + exp(-PBOT1_SCORING_SLOPE * (t - PBOT1_SCORING_OFFSET))))
	return int(PBOT1_MAX_SCORE * factor + PBOT1_MIN_SCORE)

static func _judge_note_pbot1(ms_timing: float) -> String:
	var t := absf(ms_timing)
	if t <= PBOT1_SICK_THRESHOLD:  return "sick"
	if t <= PBOT1_GOOD_THRESHOLD:  return "good"
	if t <= PBOT1_BAD_THRESHOLD:   return "bad"
	if t <= PBOT1_SHIT_THRESHOLD:  return "shit"
	push_warning("Missed note: Bad timing (%.2f ms)" % t)
	return "miss"

# ==============================================================================
# Legacy internals
# ==============================================================================

static func _score_note_legacy(ms_timing: float) -> int:
	var t := absf(ms_timing)
	if t < LEGACY_HIT_WINDOW * LEGACY_SICK_THRESHOLD: return LEGACY_SICK_SCORE
	if t < LEGACY_HIT_WINDOW * LEGACY_GOOD_THRESHOLD: return LEGACY_GOOD_SCORE
	if t < LEGACY_HIT_WINDOW * LEGACY_BAD_THRESHOLD:  return LEGACY_BAD_SCORE
	if t < LEGACY_HIT_WINDOW * LEGACY_SHIT_THRESHOLD: return LEGACY_SHIT_SCORE
	return 0

static func _judge_note_legacy(ms_timing: float) -> String:
	var t := absf(ms_timing)
	if t <= LEGACY_HIT_WINDOW * LEGACY_SICK_THRESHOLD: return "sick"
	if t <= LEGACY_HIT_WINDOW * LEGACY_GOOD_THRESHOLD: return "good"
	if t <= LEGACY_HIT_WINDOW * LEGACY_BAD_THRESHOLD:  return "bad"
	if t <= LEGACY_HIT_WINDOW * LEGACY_SHIT_THRESHOLD: return "shit"
	push_warning("Missed note: Bad timing (%.2f ms)" % t)
	return "miss"

# ==============================================================================
# Week 7 internals
# ==============================================================================

static func _score_note_week7(ms_timing: float) -> int:
	var t := absf(ms_timing)
	if t < WEEK7_HIT_WINDOW * WEEK7_SICK_THRESHOLD: return WEEK7_SICK_SCORE
	if t < WEEK7_HIT_WINDOW * WEEK7_GOOD_THRESHOLD: return WEEK7_GOOD_SCORE
	if t < WEEK7_HIT_WINDOW * WEEK7_BAD_THRESHOLD:  return WEEK7_BAD_SCORE
	if t < WEEK7_HIT_WINDOW:                        return WEEK7_SHIT_SCORE
	return 0

static func _judge_note_week7(ms_timing: float) -> String:
	var t := absf(ms_timing)
	if t <= WEEK7_HIT_WINDOW * WEEK7_SICK_THRESHOLD: return "sick"
	if t <= WEEK7_HIT_WINDOW * WEEK7_GOOD_THRESHOLD: return "good"
	if t <= WEEK7_HIT_WINDOW * WEEK7_BAD_THRESHOLD:  return "bad"
	if t <= WEEK7_HIT_WINDOW:                        return "shit"
	push_warning("Missed note: Bad timing (%.2f ms)" % t)
	return "miss"
