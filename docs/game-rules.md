# SobeSobe Game Rules

A comprehensive guide to the Portuguese trick-taking card game "SobeSobe" (literally "Go Up, Go Down").

**Table of Contents**
- [Overview](#overview)
- [Glossary of Portuguese Terms](#glossary-of-portuguese-terms)
- [Setup Phase](#setup-phase)
- [Round Phase](#round-phase)
- [Trick-Taking Phase](#trick-taking-phase)
- [Scoring Phase](#scoring-phase)
- [Game State Transitions](#game-state-transitions)
- [Gameplay Examples](#gameplay-examples)
- [Edge Cases and Special Scenarios](#edge-cases-and-special-scenarios)
- [Validation Rules](#validation-rules)

---

## Overview

SobeSobe is a Portuguese trick-taking card game where players attempt to be the first to reduce their score from 20 points down to 0 points by winning tricks. The game features a trump suit mechanic, strategic card exchange, and complex scoring rules that reward skillful play.

**Core Mechanics:**
- **Players:** 2-5 (optimal: 5)
- **Objective:** First to reach 0 points wins
- **Starting Points:** 20 per player
- **Point Reduction:** Win tricks to decrease points
- **Win Condition:** First player to 0 or fewer points
- **Prize Calculation:** Each remaining point from other players = 5 cents

---

## Glossary of Portuguese Terms

| Portuguese Term | English Translation | Description |
|----------------|-------------------|-------------|
| **Manilha** | The Seven | The 7 card, second-highest rank after Ace |
| **Copas às escuras** | Hearts in the dark | Choosing Hearts as trump before seeing any cards |
| **Cortar** | To cut | Playing a trump card when unable to follow suit |
| **Trunfo** | Trump | The suit designated as trump for the round |
| **Volta** | Round | A complete game round including dealing, playing, and scoring |
| **Vaza** | Trick | A set of cards played, one from each player |
| **Dar cartas** | Deal cards | The act of distributing cards |
| **Trocar cartas** | Exchange cards | The card-swapping phase after trump selection |
| **Mão** | Hand | The cards held by a player |

---

## Setup Phase

### Deck Composition

**40-Card Deck:**
- Start with a standard 52-card deck
- Remove all 8's, 9's, and 10's
- Final deck: 40 cards (10 per suit)

### Card Ranking (Highest to Lowest)

1. **Ace** (highest)
2. **7** (Manilha)
3. **King**
4. **Queen**
5. **Jack**
6. **6**
7. **5**
8. **4**
9. **3**
10. **2** (lowest)

> **Important:** This ranking applies within each suit. Trump cards always beat non-trump cards regardless of rank.

### Initial Game State

- Each player starts with **20 points**
- First dealer is chosen randomly
- Dealer position rotates **counter-clockwise** after each round

---

## Round Phase

### 1. Dealer Selection

- The dealer is designated at the start of each round
- After the first round, the dealer position rotates counter-clockwise
- The dealer is responsible for:
  - Shuffling the deck
  - Dealing cards to all players
  - Managing the card exchange phase

### 2. Party Player Designation

The **party player** is always the player to the **right** of the dealer (counter-clockwise direction). The party player has special privileges and responsibilities:
- Decides the trump suit for the round
- **Must** play in the round (cannot opt out)
- Receives **double penalties** if they make no tricks

### 3. Initial Card Deal

**Dealing sequence (always counter-clockwise):**
1. Dealer deals **2 cards** to the party player (to their right)
2. Continue counter-clockwise, dealing 2 cards to each player
3. Dealer receives 2 cards last

### 4. Trump Suit Selection

The party player chooses the trump suit for the round. They have two options:

#### Option A: Before Dealing (Blind Trump)
- Party player announces trump **before** any cards are dealt
- **Effect:** All trick values are **doubled**
- Any suit can be chosen
- Special name if Hearts is chosen: "Copas às escuras" (Hearts in the dark)

#### Option B: After Receiving 2 Cards
- Party player looks at their 2 cards first
- Then announces the trump suit
- **Effect:** Normal trick values (not doubled)

**Available Trump Suits:**
- **Hearts** ❤️ - Trick value: 2 points (4 if chosen before dealing)
- **Diamonds** ♦️ - Trick value: 1 point (2 if chosen before dealing)
- **Clubs** ♣️ - Trick value: 1 point (2 if chosen before dealing)
- **Spades** ♠️ - Trick value: 1 point (2 if chosen before dealing)

### 5. Player Decision Phase (Play or Sit Out)

After trump is declared, each player (except the party player) decides whether to play in the round.

**Mandatory Play Conditions (Cannot Sit Out):**
1. **Party player** - Must always play
2. **5 Points or Less** - Any player with ≤5 points must play
3. **Clubs Trump** - If trump is Clubs ♣️, all players must play
4. **Consecutive Sit-Outs** - No player can sit out more than 2 consecutive rounds

**Decision Order:** Counter-clockwise, starting from party player (who always plays)

### 6. Final Card Deal

Players who decided to play in the round receive **3 additional cards**, bringing their total to **5 cards**.

**Dealing order:** Counter-clockwise, starting from party player

### 7. Card Exchange Phase

Each player who is playing the round can exchange up to **3 cards** from their hand.

**Exchange Rules:**
- Exchange order: Counter-clockwise, starting from **party player**
- Dealer exchanges last
- Players discard cards face-down and receive replacements from the deck
- Number of cards exchanged: 0-3 (player's choice)
- Exchanged cards are shuffled back into the remaining deck

**Strategy Note:** The card exchange phase is critical for improving your hand before trick-taking begins.

---

## Trick-Taking Phase

A round consists of exactly **5 tricks** (since each player has 5 cards).

### Lead Player

- **First trick:** Party player leads (plays first)
- **Subsequent tricks:** Winner of previous trick leads

### Playing Cards - Core Rules

1. **Follow Suit:** Players must play a card of the same suit as the lead card if they have one
2. **Cannot Follow Suit:**
   - If player has a trump card, they **must** play a trump (called "cortar" - to cut)
   - If no trump, player can play any card
3. **Trump Escalation:** When a trump card is led:
   - All players must play a **higher** trump card than previously played if they can
   - If cannot play higher trump, must still play trump if they have any
   - If no trump at all, can play any card

### Special Rule: Mandatory Ace of Trump

**Critical Rule:** If a player has the **Ace of the trump suit**, they **must** play it at the first legal opportunity.

**Exception:** The Ace of trump does NOT need to be played when doing a cut (cortar). It only needs to be played when:
- It's your turn and you can legally lead it
- Trump is led and you must follow

**Example:**
- Trump is Hearts ❤️
- You have Ace of Hearts in hand
- Lead card is Diamonds ♦️ and you have diamonds
- You must follow suit with a diamond (not forced to cut with Ace of Hearts)
- But once you're out of diamonds, you must play Ace of Hearts when next opportunity arises

### Determining Trick Winner

The trick winner is determined by:
1. **Highest trump card played** (if any trump cards were played)
2. **Highest card of the suit led** (if no trump cards were played)

**Examples:**
- Lead: 5♦️, Plays: K♦️, 3♠️ (trump), 7♦️, 2♣️ → Winner: 3♠️ (only trump)
- Lead: J♥️ (trump), Plays: K♥️, A♥️, 7♥️, Q♥️ → Winner: A♥️ (highest trump)
- Lead: Q♣️, Plays: K♣️, 7♣️, A♣️, 2♦️ → Winner: A♣️ (highest of suit led)

### Trick Flow

1. Lead player plays a card face-up
2. Each player plays one card in counter-clockwise order
3. Trick winner is determined
4. Trick cards are collected and set aside
5. Trick winner leads the next trick
6. Repeat until all 5 tricks are played

---

## Scoring Phase

### Point Reduction (Winning Tricks)

Players who played in the round and won tricks reduce their points:
- **Points reduced** = Number of tricks won × Trick value

**Trick Values:**
| Trump Suit | Timing | Points per Trick |
|-----------|--------|------------------|
| Hearts ❤️ | After 2 cards | 2 |
| Hearts ❤️ | Before dealing | 4 |
| Diamonds ♦️ | After 2 cards | 1 |
| Diamonds ♦️ | Before dealing | 2 |
| Clubs ♣️ | After 2 cards | 1 |
| Clubs ♣️ | Before dealing | 2 |
| Spades ♠️ | After 2 cards | 1 |
| Spades ♠️ | Before dealing | 2 |

### Penalties (Making No Tricks)

If a player played in the round and won **zero tricks**, they receive a penalty:

| Trick Value | Penalty (Normal) | Penalty (Party Player) |
|------------|------------------|------------------------|
| 1 point | +5 points | +10 points |
| 2 points | +10 points | +20 points |
| 4 points | +20 points | +40 points |

> **Critical:** The party player receives **double penalties** for making no tricks.

### Players Who Sat Out

Players who did not play the round:
- **No point change** (points remain the same)
- Indicated in score table with a dash "-" or grayed-out cell

### Round Score Examples

**Example 1: Normal Round**
- Trump: Diamonds ♦️ (after 2 cards) = 1 point per trick
- Players: Alice, Bob, Charlie, Dana, Eve
- Party player: Alice
- Tricks won: Alice (2), Bob (1), Charlie (2), Dana (0), Eve (sat out)

**Score changes:**
- Alice: -2 points (2 tricks × 1 point)
- Bob: -1 point (1 trick × 1 point)
- Charlie: -2 points (2 tricks × 1 point)
- Dana: +5 points (penalty for 0 tricks)
- Eve: 0 points (sat out)

**Example 2: High-Stakes Round**
- Trump: Hearts ❤️ chosen before dealing = 4 points per trick
- Party player: Bob
- Tricks won: Alice (1), Bob (0), Charlie (2), Dana (2)

**Score changes:**
- Alice: -4 points (1 trick × 4 points)
- Bob: +40 points (party player penalty, doubled from +20)
- Charlie: -8 points (2 tricks × 4 points)
- Dana: -8 points (2 tricks × 4 points)

---

## Game State Transitions

```
┌─────────────┐
│ Game Start  │ Each player: 20 points
└──────┬──────┘
       │
       v
┌──────────────────┐
│ Round Start      │ Dealer designated, deck shuffled
└──────┬───────────┘
       │
       v
┌──────────────────┐
│ Initial Deal     │ 2 cards to each player (counter-clockwise)
└──────┬───────────┘
       │
       v
┌──────────────────┐
│ Trump Selection  │ Party player chooses trump suit
└──────┬───────────┘
       │
       v
┌──────────────────┐
│ Player Decisions │ Each player decides to play or sit out
└──────┬───────────┘
       │
       v
┌──────────────────┐
│ Final Deal       │ 3 more cards to active players (5 total)
└──────┬───────────┘
       │
       v
┌──────────────────┐
│ Card Exchange    │ Active players exchange up to 3 cards
└──────┬───────────┘
       │
       v
┌──────────────────┐
│ Trick 1          │ Party player leads
└──────┬───────────┘
       │
       v
┌──────────────────┐
│ Tricks 2-5       │ Previous winner leads
└──────┬───────────┘
       │
       v
┌──────────────────┐
│ Scoring          │ Points adjusted based on tricks won
└──────┬───────────┘
       │
       v
    ┌──┴──┐
    │ Win? │ Any player ≤ 0 points?
    └──┬──┘
       │
   ┌───┴───┐
   │ Yes   │ No
   v       v
┌────────┐ ┌──────────────┐
│ Game   │ │ Next Round   │ Dealer rotates counter-clockwise
│ End    │ └──────┬───────┘
└────────┘        │
                  └────────> (back to Round Start)
```

---

## Gameplay Examples

### Example Game: 3 Players

**Players:** Alice, Bob, Charlie  
**Starting Points:** Alice: 20, Bob: 20, Charlie: 20

#### Round 1
- **Dealer:** Alice
- **Party Player:** Charlie (to Alice's right, counter-clockwise)
- **Trump Selection:** Charlie looks at 2 cards, chooses Spades ♠️
- **Trick Value:** 1 point
- **Player Decisions:** All play
- **Card Exchange:** Charlie exchanges 2, Alice exchanges 1, Bob exchanges 3
- **Tricks Won:** Charlie: 2, Alice: 2, Bob: 1

**Score Update:**
- Charlie: 20 - 2 = 18
- Alice: 20 - 2 = 18
- Bob: 20 - 1 = 19

#### Round 2
- **Dealer:** Charlie (rotated counter-clockwise)
- **Party Player:** Bob
- **Trump Selection:** Bob chooses Hearts ❤️ before dealing
- **Trick Value:** 4 points (doubled because chosen before dealing)
- **Player Decisions:** All play
- **Tricks Won:** Bob: 0, Charlie: 3, Alice: 2

**Score Update:**
- Bob: 19 + 40 = 59 (party player penalty, doubled)
- Charlie: 18 - 12 = 6 (3 tricks × 4 points)
- Alice: 18 - 8 = 10 (2 tricks × 4 points)

#### Round 3
- **Dealer:** Bob
- **Party Player:** Alice
- **Trump Selection:** Alice chooses Diamonds ♦️ after 2 cards
- **Trick Value:** 1 point
- **Player Decisions:** Charlie must play (≤5 points), Alice plays (party), Bob sits out (2 consecutive sit-outs remaining)
- **Tricks Won:** Charlie: 3, Alice: 2

**Score Update:**
- Charlie: 6 - 3 = 3
- Alice: 10 - 2 = 8
- Bob: 59 (no change, sat out)

#### Round 4
- **Dealer:** Alice
- **Party Player:** Charlie
- **Trump Selection:** Charlie chooses Clubs ♣️
- **Trick Value:** 1 point
- **Player Decisions:** All must play (Clubs trump = mandatory)
- **Tricks Won:** Charlie: 2, Alice: 2, Bob: 1

**Score Update:**
- Charlie: 3 - 2 = **1**
- Alice: 8 - 2 = 6
- Bob: 59 - 1 = 58

#### Round 5
- **Dealer:** Charlie
- **Party Player:** Bob
- **Trump Selection:** Bob chooses Diamonds ♦️ after 2 cards
- **Trick Value:** 1 point
- **Player Decisions:** Charlie must play (≤5 points), Bob plays (party), Alice plays
- **Tricks Won:** Charlie: 3, Bob: 1, Alice: 1

**Score Update:**
- Charlie: 1 - 3 = **-2** ✅ **WINNER!**
- Bob: 58 - 1 = 57
- Alice: 6 - 1 = 5

**Game Over:** Charlie wins!  
**Prize:** Bob has 57 points × €0.05 = €2.85, Alice has 5 points × €0.05 = €0.25  
**Total Prize:** €2.85 + €0.25 = **€3.10**

---

## Edge Cases and Special Scenarios

### 1. Multiple Players Reach 0 in Same Round

**Scenario:** Alice ends with -1, Bob ends with 0 in the same round.

**Resolution:** The player with the **lowest** score wins. If tied at exactly 0, the player who won more tricks in that round wins. If still tied, they share the victory.

### 2. Trump Ace Mandatory Play vs. Following Suit

**Scenario:** You have Ace of Hearts (trump is Hearts), lead card is Diamonds, and you have diamonds in hand.

**Resolution:** You **must follow suit** with a diamond. The Ace of trump rule only applies when you can legally play it (i.e., when trump is led, or you're leading).

### 3. Player Cannot Follow Suit or Play Trump

**Scenario:** Lead is Spades, trump is Hearts, you have neither spades nor hearts.

**Resolution:** You can play **any card** from your hand. This card cannot win the trick unless no one else followed suit or played trump.

### 4. Trump Escalation with No Higher Trump

**Scenario:** Trump is Clubs, 7♣️ (Manilha, second-highest) is played, you only have 2♣️.

**Resolution:** You must still play your 2♣️ (you must play trump if you have it, even if it can't win).

### 5. All Players Sit Out Except Party Player

**Scenario:** Only the party player decides to play (others sit out or are ineligible).

**Resolution:** Round **cannot proceed**. At least 2 players must play. If this happens, trump selection is reset and player decisions restart.

### 6. Player Forgets to Exchange Cards

**Scenario:** A player's turn to exchange comes, but they don't exchange (timeout or skip).

**Resolution:** Treated as exchanging 0 cards. Play continues with their current hand.

### 7. Consecutive Sit-Out Tracking

**Scenario:** Alice sits out Round 1 and 2. Can she sit out Round 3?

**Resolution:** **No.** She must play Round 3 (maximum 2 consecutive sit-outs). Counter resets to 0 if she plays.

### 8. Party Player with ≤5 Points

**Scenario:** Party player has 4 points. Do double penalties still apply?

**Resolution:** **Yes.** Party player always receives double penalties for making no tricks, regardless of their point total.

### 9. Trump Chosen Before Dealing + Clubs

**Scenario:** Party player chooses Clubs ♣️ before dealing.

**Resolution:**
- Trick value is 2 points (1 × 2 for choosing before dealing)
- All players must still play (Clubs rule)
- Both rules apply simultaneously

### 10. Negative Points

**Scenario:** Player has 2 points, wins 3 tricks worth 2 points each.

**Resolution:** Player ends with 2 - 6 = **-4 points**. This is valid and they win the game.

---

## Validation Rules

### During Trump Selection
- ✅ Party player can choose any suit (Hearts, Diamonds, Clubs, Spades)
- ✅ Trump can be chosen before dealing or after receiving 2 cards
- ❌ Non-party players cannot choose trump
- ❌ Cannot change trump after it's declared

### During Player Decision Phase
- ✅ Party player always plays (no decision needed)
- ✅ Players with ≤5 points must play
- ✅ All players must play if trump is Clubs
- ❌ Cannot sit out more than 2 consecutive rounds
- ✅ Players not meeting mandatory conditions can choose to sit out

### During Card Exchange
- ✅ Can exchange 0-3 cards
- ❌ Cannot exchange more than 3 cards
- ❌ Cannot exchange cards after exchange phase ends
- ✅ Exchange order is always counter-clockwise from party player

### During Trick-Taking
- ✅ Must follow suit if possible
- ✅ Must play trump if cannot follow suit and have trump
- ✅ Must play higher trump if trump is led and you can
- ✅ Must play Ace of trump at first legal opportunity (except when cutting)
- ❌ Cannot play out of turn
- ❌ Cannot play card of wrong suit when you have the led suit
- ❌ Cannot withhold Ace of trump when you should play it

### Scoring
- ✅ Points can go negative
- ✅ First player to ≤0 wins
- ✅ Party player penalties are always doubled
- ❌ Cannot reduce points if you sat out
- ❌ Cannot avoid penalty if you played and won 0 tricks

---

## UI/UX Requirements for Score Table

The score table should visually represent the game progression:

```
┌──────────┬────────┬────────┬────────┬────────┬────────┐
│ Round    │ Alice  │ Bob    │ Charlie│ Dana   │ Eve    │
├──────────┼────────┼────────┼────────┼────────┼────────┤
│ Start    │ 20     │ 20     │ 20     │ 20     │ 20     │
├──────────┼────────┼────────┼────────┼────────┼────────┤
│ 1        │ 18     │ 19     │ 18     │ 22     │ -      │
├──────────┼────────┼────────┼────────┼────────┼────────┤
│ 2        │ 10     │ 59     │ 6      │ -      │ 22     │
├──────────┼────────┼────────┼────────┼────────┼────────┤
│ 3        │ 8      │ -      │ 3      │ 15     │ 20     │
└──────────┴────────┴────────┴────────┴────────┴────────┘
```

**Visual indicators:**
- **Bold/Highlighted:** Current dealer
- **Underlined:** Party player for that round
- **"-" or grayed out:** Player sat out that round
- **Red text:** Points increased (penalty)
- **Green text:** Points decreased (won tricks)
- **Gold border:** Winner (first to ≤0)

---

## Summary

SobeSobe is a strategic trick-taking game where:
1. Players aim to reduce their 20 starting points to 0
2. Trump suit adds strategic depth
3. Players can sit out rounds (with limits)
4. Card exchange allows hand optimization
5. Strict following and trump escalation rules
6. Penalties punish poor play (especially for party player)
7. First to 0 or below wins and collects prize money based on others' remaining points

The combination of trump selection timing, opt-in/opt-out decisions, card exchange, mandatory Ace rules, and asymmetric party player penalties creates a rich strategic environment where risk management and tactical play are essential to victory.
