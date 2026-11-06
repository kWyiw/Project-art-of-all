using System;
using System.Collections.Generic;
using ProjectArtOfAll.Core.Match;

namespace ProjectArtOfAll.Flow
{
    /// <summary>What occupies a lobby seat.</summary>
    public enum LobbySeatKind
    {
        Empty = 0,

        /// <summary>The player sitting at this machine.</summary>
        LocalHuman = 1,

        /// <summary>A human on another client — reserved for the online build (M6).</summary>
        RemoteHuman = 2,

        Cpu = 3
    }

    /// <summary>One seat of the ready room. Mutable only through <see cref="LobbyState"/>.</summary>
    public sealed class LobbySeat
    {
        internal LobbySeat(int index)
        {
            Index = index;
            Kind = LobbySeatKind.Empty;
            Archetype = CpuArchetype.Aggressive;
            Difficulty = CpuDifficulty.Normal;
        }

        public int Index { get; }

        public LobbySeatKind Kind { get; internal set; }

        public CpuArchetype Archetype { get; internal set; }

        public CpuDifficulty Difficulty { get; internal set; }

        public bool Ready { get; internal set; }

        public bool IsHuman =>
            Kind == LobbySeatKind.LocalHuman || Kind == LobbySeatKind.RemoteHuman;

        public bool IsEmpty => Kind == LobbySeatKind.Empty;

        /// <summary>Player id used by the match kernel; identical to the seat index.</summary>
        public int PlayerId => Index;

        public string Title => "席位 " + (Index + 1);

        public string OccupantLabel
        {
            get
            {
                switch (Kind)
                {
                    case LobbySeatKind.LocalHuman:
                        return Index == LobbyState.HostSeat ? "你 · 房主" : "你";
                    case LobbySeatKind.RemoteHuman:
                        return "远程玩家 等待加入";
                    case LobbySeatKind.Cpu:
                        return LobbyState.ArchetypeLabel(Archetype)
                               + " · " + LobbyState.DifficultyLabel(Difficulty);
                    default:
                        return "空席位";
                }
            }
        }
    }

    /// <summary>
    /// Ready-room state: four seats, the local player owns seat 0 and hosts the room.
    /// Online play (M6) only has to call <see cref="SetRemoteHuman"/> and mirror
    /// <see cref="ToggleReady"/> — nothing here touches the kernel.
    /// </summary>
    public static class LobbyState
    {
        public const int SeatCount = 4;
        public const int HostSeat = 0;

        private static readonly CpuArchetype[] Archetypes =
        {
            CpuArchetype.Aggressive,
            CpuArchetype.Economist,
            CpuArchetype.Guardian,
            CpuArchetype.Hunter
        };

        private static readonly CpuDifficulty[] Difficulties =
        {
            CpuDifficulty.Easy,
            CpuDifficulty.Normal,
            CpuDifficulty.Hard
        };

        private static LobbySeat[] seats;

        /// <summary>Raised after any change, so the ready-room screen can repaint.</summary>
        public static event Action Changed;

        public static IReadOnlyList<LobbySeat> Seats
        {
            get
            {
                EnsureCreated();
                return seats;
            }
        }

        public static LobbySeat Get(int index)
        {
            EnsureCreated();
            if (index < 0 || index >= SeatCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return seats[index];
        }

        /// <summary>Fresh room: the local player sits at the host seat, the rest is empty.</summary>
        public static void Reset()
        {
            seats = new LobbySeat[SeatCount];
            for (int index = 0; index < SeatCount; index++)
            {
                seats[index] = new LobbySeat(index);
            }

            seats[HostSeat].Kind = LobbySeatKind.LocalHuman;
            seats[HostSeat].Ready = true;
            Notify();
        }

        /// <summary>Put a CPU on an empty seat with a default (rotating) personality.</summary>
        public static bool AddCpu(int index)
        {
            LobbySeat seat = Get(index);
            if (!seat.IsEmpty)
            {
                return false;
            }

            seat.Kind = LobbySeatKind.Cpu;
            seat.Archetype = Archetypes[index % Archetypes.Length];
            seat.Difficulty = GameSettings.CpuDifficultyTier;
            seat.Ready = true;
            Notify();
            return true;
        }

        public static bool SetCpu(int index, CpuArchetype archetype, CpuDifficulty difficulty)
        {
            LobbySeat seat = Get(index);
            if (seat.Kind != LobbySeatKind.Cpu)
            {
                return false;
            }

            seat.Archetype = archetype;
            seat.Difficulty = difficulty;
            Notify();
            return true;
        }

        /// <summary>Clear a seat. The local human seat can never be removed.</summary>
        public static bool Remove(int index)
        {
            LobbySeat seat = Get(index);
            if (seat.Kind == LobbySeatKind.LocalHuman)
            {
                return false;
            }

            seat.Kind = LobbySeatKind.Empty;
            seat.Ready = false;
            seat.Archetype = Archetypes[index % Archetypes.Length];
            seat.Difficulty = CpuDifficulty.Normal;
            Notify();
            return true;
        }

        public static bool CycleArchetype(int index, int delta)
        {
            LobbySeat seat = Get(index);
            if (seat.Kind != LobbySeatKind.Cpu)
            {
                return false;
            }

            int current = Array.IndexOf(Archetypes, seat.Archetype);
            int next = (current + delta) % Archetypes.Length;
            if (next < 0)
            {
                next += Archetypes.Length;
            }

            seat.Archetype = Archetypes[next];
            Notify();
            return true;
        }

        public static bool CycleDifficulty(int index, int delta)
        {
            LobbySeat seat = Get(index);
            if (seat.Kind != LobbySeatKind.Cpu)
            {
                return false;
            }

            int current = Array.IndexOf(Difficulties, seat.Difficulty);
            int next = (current + delta) % Difficulties.Length;
            if (next < 0)
            {
                next += Difficulties.Length;
            }

            seat.Difficulty = Difficulties[next];
            Notify();
            return true;
        }

        /// <summary>Reserve a seat for a remote client (M6); keeps the room shape intact.</summary>
        public static bool SetRemoteHuman(int index)
        {
            LobbySeat seat = Get(index);
            if (seat.Kind == LobbySeatKind.LocalHuman)
            {
                return false;
            }

            seat.Kind = LobbySeatKind.RemoteHuman;
            seat.Ready = false;
            Notify();
            return true;
        }

        public static bool ToggleReady(int index)
        {
            LobbySeat seat = Get(index);
            if (!seat.IsHuman)
            {
                return false;
            }

            seat.Ready = !seat.Ready;
            Notify();
            return true;
        }

        public static void SetReady(int index, bool ready)
        {
            LobbySeat seat = Get(index);
            if (!seat.IsHuman)
            {
                return;
            }

            if (seat.Ready == ready)
            {
                return;
            }

            seat.Ready = ready;
            Notify();
        }

        public static int FilledSeatCount
        {
            get
            {
                EnsureCreated();
                int count = 0;
                for (int index = 0; index < SeatCount; index++)
                {
                    if (!seats[index].IsEmpty)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public static bool AllSeatsFilled => FilledSeatCount == SeatCount;

        public static bool AllHumansReady
        {
            get
            {
                EnsureCreated();
                for (int index = 0; index < SeatCount; index++)
                {
                    LobbySeat seat = seats[index];
                    if (seat.IsHuman && !seat.Ready)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>Host may start once every seat is occupied and every human is ready.</summary>
        public static bool CanStart => AllSeatsFilled && AllHumansReady;

        public static int LocalHumanSeat
        {
            get
            {
                EnsureCreated();
                for (int index = 0; index < SeatCount; index++)
                {
                    if (seats[index].Kind == LobbySeatKind.LocalHuman)
                    {
                        return index;
                    }
                }

                return HostSeat;
            }
        }

        /// <summary>Kernel personality for a seat; empty seats fall back to the seat mapping.</summary>
        public static CpuPersonality PersonalityFor(int index)
        {
            LobbySeat seat = Get(index);
            if (seat.Kind == LobbySeatKind.Cpu)
            {
                return CpuPersonality.Create(seat.Archetype, seat.Difficulty);
            }

            return CpuPersonality.ForSeat(index, CpuDifficulty.Normal, SeatCount);
        }

        public static string ArchetypeLabel(CpuArchetype archetype)
        {
            switch (archetype)
            {
                case CpuArchetype.Aggressive:
                    return "激进";
                case CpuArchetype.Economist:
                    return "理财";
                case CpuArchetype.Guardian:
                    return "保守";
                case CpuArchetype.Hunter:
                    return "狩猎";
                default:
                    return archetype.ToString();
            }
        }

        public static string DifficultyLabel(CpuDifficulty difficulty)
        {
            switch (difficulty)
            {
                case CpuDifficulty.Easy:
                    return "新手";
                case CpuDifficulty.Hard:
                    return "困难";
                default:
                    return "普通";
            }
        }

        private static void EnsureCreated()
        {
            if (seats == null)
            {
                seats = new LobbySeat[SeatCount];
                for (int index = 0; index < SeatCount; index++)
                {
                    seats[index] = new LobbySeat(index);
                }

                seats[HostSeat].Kind = LobbySeatKind.LocalHuman;
                seats[HostSeat].Ready = true;
            }
        }

        private static void Notify()
        {
            Changed?.Invoke();
        }
    }
}
