# Unity Pacman Capture the Flag 

---

## Project Overview

This project implements an AI for a **Pacman Capture the Flag** game, set in a symmetric maze environment where two teams compete. Each team controls multiple agents (“ghosts”) trying to collect enemy pills from the opponent's side and bring them back safely to their own side. The team with the most pills on their side at the end wins.

- When agents cross into the opponent’s half, they transform into Pacman and can eat pills.
- Eating a super-pill on the enemy side enables temporarily eating ghosts.
- Agents drop carried pills if killed, and re-spawn on their own side.
- Enemy sensing is done via line-of-sight and noisy audio cues, adding strategic uncertainty.

---

## ⚙️ Implementation Details

### Roles & Strategy

We divided the agents into two roles:

- **Attackers:** Focused on collecting food clusters on the enemy side and returning safely with the pills.
- **Defenders:** Responsible for guarding the home side through patrolling, chasing intruders, and returning to patrol.

### Map Preprocessing

- We precomputed **all-pairs shortest paths** on the maze using the Floyd–Warshall algorithm.
- Distance and predecessor matrices are stored for quick path reconstruction during gameplay.

### Central Game Tracker

- Coordinates agent roles and assigns attackers and defenders based on team size and game state.
- Identifies clusters of food pills and assigns them to attackers, prioritizing those farther from the border.
- Manages defenders’ state transitions (Patrol, Chase, Return) dynamically.

### Attacker Behavior

- One or two attackers assigned per team, depending on the number of agents.
- Attackers follow precomputed paths to assigned clusters to collect pills.
- Return paths consider enemy positions to choose the safest border crossing.
- Attackers move at maximum speed, emphasizing fast pill collection over stealth.

### Defender Behavior

- Defenders patrol assigned positions near the border, moving in a pattern parallel to the dividing line.
- On enemy detection, the nearest defender chases by predicting enemy movement, using direct movement if possible.
- After chasing or respawn, defenders return to their patrol positions.

---

## 📘 Course Information

This project was developed for **Assignment 3** of the course  
**DD2438 – Autonomous Agents and Multi-Agent Systems** at **KTH Royal Institute of Technology**.

The Unity environment, including pacman models and map assets, was provided as part of the assignment.  
Our work focused on implementing custom path planning and strategies.

## 👤 Authors

- Chloé Leyssens  
- Emil Wiktorsson
