using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Reflection;
using DOL.Database;
using System.Runtime.InteropServices;
using DOL.GS;
using DOL.GS.Scripts;
using DOL.GS.ServerProperties;
using log4net;

namespace DOL.AI.Brain
{
    public class MimicState : FSMState
    {
        protected static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        protected MimicBrain _brain = null;

        public bool Init;

        public MimicState(MimicBrain brain) : base()
        {
            _brain = brain;
        }

        public override void Think()
        { }

        public override void Enter()
        { }

        public override void Exit()
        { }
    }

    public class MimicState_WakingUp : MimicState
    {
        public MimicState_WakingUp(MimicBrain brain) : base(brain)
        {
            StateType = eFSMStateType.WAKING_UP;
        }

        public override void Think()
        {
            if (!Init)
            {
                _brain.AggroLevel = 100;
                _brain.AggroRange = 3600;

                _brain.PvPMode = _brain.Body.CurrentRegion.IsRvR || _brain.Body.CurrentZone.IsRvR;

                _brain.Roam = true;
                _brain.Defend = false;

                _brain.Body.RoamingRange = 100000;

                //_brain.CheckDefensiveAbilities();
                //_brain.Body.SortSpells();

                Init = true;
            }

            if (_brain.Body.Group != null)
            {
                if (_brain.Body.Group.MimicGroup.CampPoint != null && _brain.Body.IsWithinRadius(_brain.Body.Group?.MimicGroup.CampPoint, 1500))
                {
                    _brain.FSM.SetCurrentState(eFSMStateType.CAMP);
                    return;
                }
                else if (_brain.Body.Group.LivingLeader != _brain.Body)
                {
                    _brain.FSM.SetCurrentState(eFSMStateType.FOLLOW_THE_LEADER);
                    return;
                }
            }

            if (!_brain.Body.attackComponent.AttackState && _brain.Body.CanRoam)
            {
                _brain.FSM.SetCurrentState(eFSMStateType.ROAMING);
                return;
            }

            if (_brain.HasPatrolPath())
            {
                _brain.FSM.SetCurrentState(eFSMStateType.PATROLLING);
                return;
            }

            if (!_brain.PreventCombat)
            {
                if (_brain.CheckProximityAggro(_brain.AggroRange))
                {
                    _brain.FSM.SetCurrentState(eFSMStateType.AGGRO);
                    return;
                }
            }

            _brain.FSM.SetCurrentState(eFSMStateType.IDLE);
            base.Think();
        }
    }

    public class MimicState_Idle : MimicState
    {
        public MimicState_Idle(MimicBrain brain) : base(brain)
        {
            StateType = eFSMStateType.IDLE;
        }

        public override void Enter()
        {
            base.Enter();
        }

        public override void Think()
        {
            //if (_brain.HasPatrolPath())
            //{
            //    _brain.FSM.SetCurrentState(eFSMStateType.PATROLLING);
            //    return;
            //}

            //if (_brain.Body.CanRoam)
            //{
            //    _brain.FSM.SetCurrentState( eFSMStateType.ROAMING);
            //    return;
            //}

            //if (_brain.IsBeyondTetherRange())
            //{
            //    _brain.FSM.SetCurrentState(eFSMStateType.RETURN_TO_SPAWN);
            //    return;
            //}

            //if (!_brain.PreventCombat)
            //{
            //    if (_brain.CheckProximityAggro(_brain.AggroRange))
            //    {
            //        _brain.FSM.SetCurrentState(eFSMStateType.AGGRO);
            //        return;
            //    }
            //}

            _brain.CheckSpells(MimicBrain.eCheckSpellType.Defensive);

            base.Think();
        }
    }

    public class MimicState_FollowLeader : MimicState
    {
        private GameLiving _leader;
        private int _followDistance;
        private int _targetFollowDistance => 80 + _brain.Body.GroupIndex * 20;

        public MimicState_FollowLeader(MimicBrain brain) : base(brain)
        {
            StateType = eFSMStateType.FOLLOW_THE_LEADER;
        }

        public override void Enter()
        {
            if (_brain.Body.Group != null)
            {
                _leader = _brain.Body.Group.LivingLeader;
                _followDistance = _targetFollowDistance;
                _brain.Body.Follow(_brain.Body.Group.LivingLeader, _followDistance, 5000);
            }
            else
                _brain.FSM.SetCurrentState(eFSMStateType.WAKING_UP);

            base.Enter();
        }

        public override void Think()
        {
            if (_leader == null || (_leader != null && _leader.ObjectState != GameObject.eObjectState.Active || !_brain.Body.Group.IsInTheGroup(_leader)))
                _leader = _brain.Body.Group.LivingLeader;

            if (_brain.Body.Group == null || _leader == _brain.Body)
            {
                _brain.Body.StopFollowing();
                _brain.FSM.SetCurrentState(eFSMStateType.WAKING_UP);

                return;
            }

            if (_followDistance != _targetFollowDistance)
            {
                _followDistance = _targetFollowDistance;
                _brain.Body.Follow(_leader, _followDistance, 5000);
            }

            if (_leader != null && ((_leader.IsCasting || _leader.IsAttacking) && _leader.TargetObject is GameLiving livingTarget && _brain.CanAggroTarget(livingTarget)))
            {
                _brain.OnLeaderAggro();
                _brain.AddToAggroList(livingTarget, 1);
                _brain.FSM.SetCurrentState(eFSMStateType.AGGRO);
                return;
            }

            if (_brain.Body.FollowTarget != _leader)
                _brain.Body.Follow(_brain.Body.Group.LivingLeader, _followDistance, 5000);

            if (!_brain.Body.InCombat)
            {
                if (_brain.Body.IsSitting && !_brain.CheckStats(75))
                    _brain.MimicBody.Sit(false);

                if (!_brain.Body.IsSitting && !_brain.Body.IsCasting && !_brain.CheckSpells(MimicBrain.eCheckSpellType.Defensive))
                    _brain.MimicBody.Sit(_brain.CheckStats(75));
            }

            base.Think();
        }

        public override void Exit()
        {
            _brain.Body.StopFollowing();

            _brain.OnExitAggro();

            base.Exit();
        }
    }

    public class MimicState_Aggro : MimicState
    {
        private const int LEAVE_WHEN_OUT_OF_COMBAT_FOR = 25000;

        private long _aggroTime = GameLoop.GameLoopTime; // Used to prevent leaving on the first think tick, due to `InCombatInLast` returning false.
        private long _checkAggroTime = GameLoop.GameLoopTime + 5000;

        public MimicState_Aggro(MimicBrain brain) : base(brain)
        {
            StateType = eFSMStateType.AGGRO;
        }

        public override void Enter()
        {
            _brain.MimicBody.Sit(false);

            _aggroTime = GameLoop.GameLoopTime;

            _brain.OnEnterAggro();

            base.Enter();
        }

        public override void Exit()
        {
            _brain.Body.StopAttack();
            _brain.Body.StopMoving();
            _brain.Body.StopCurrentSpellcast();
            _brain.ClearAggroList();
            _brain.Body.TargetObject = null;

            if (_brain.MimicBody.CharacterClass.ID == (int)eCharacterClass.Reaver)
            {
                foreach (ECSPulseEffect pulseEffect in _brain.MimicBody.effectListComponent.GetAllPulseEffects())
                {
                    EffectService.RequestImmediateCancelEffect(pulseEffect);
                }
            }

            _brain.OnExitAggro();

            base.Exit();
        }

        public override void Think()
        {
            if (_checkAggroTime < GameLoop.GameLoopTime)
            {
                _brain.CheckProximityAggro(_brain.AggroRange);
                _checkAggroTime = GameLoop.GameLoopTime + 5000;
            }

            if (!_brain.HasAggro || (!_brain.Body.InCombatInLast(LEAVE_WHEN_OUT_OF_COMBAT_FOR) && _aggroTime + LEAVE_WHEN_OUT_OF_COMBAT_FOR <= GameLoop.GameLoopTime))
            {
                if (!_brain.Body.IsMezzed && !_brain.Body.IsStunned)
                {
                    if (_brain.PvPMode)
                    {
                        if (_brain.Roam)
                        {
                            if (_brain.Body.Group != null)
                            {
                                if (_brain.Body.Group.LivingLeader == _brain.Body)
                                    _brain.FSM.SetCurrentState(eFSMStateType.ROAMING);
                                else
                                    _brain.FSM.SetCurrentState(eFSMStateType.FOLLOW_THE_LEADER);
                            }
                            else
                                _brain.FSM.SetCurrentState(eFSMStateType.ROAMING);
                        }
                        else if (_brain.Defend)
                        {
                            if (_brain.Body.Group != null)
                            {
                                if (_brain.Body.Group.LivingLeader == _brain.Body)
                                    _brain.FSM.SetCurrentState(eFSMStateType.RETURN_TO_SPAWN);
                                else
                                    _brain.FSM.SetCurrentState(eFSMStateType.FOLLOW_THE_LEADER);
                            }
                            else
                                _brain.FSM.SetCurrentState(eFSMStateType.RETURN_TO_SPAWN);
                        }
                    }
                    else
                    {
                        if (_brain.Body.Group != null)
                        {
                            if (_brain.Body.Group.MimicGroup.CampPoint != null)
                                _brain.FSM.SetCurrentState(eFSMStateType.CAMP);
                            else if (_brain.Body.Group.LivingLeader == _brain.Body)
                                _brain.FSM.SetCurrentState(eFSMStateType.WAKING_UP);
                            else
                                _brain.FSM.SetCurrentState(eFSMStateType.FOLLOW_THE_LEADER);
                        }
                        else
                            _brain.FSM.SetCurrentState(eFSMStateType.WAKING_UP);
                    }

                    return;
                }
            }

            _brain.AttackMostWanted();

            base.Think();
        }
    }

    public class MimicState_Roaming : MimicState
    {
        private long _nextRoamingTick;
        private bool _nextRoamingTickSet;
        protected virtual short Speed => _brain.Body.MaxSpeed;
        protected virtual int MinCooldown => 1;
        protected virtual int MaxCooldown => 5;

        private bool delayRoam;

        public MimicState_Roaming(MimicBrain brain) : base(brain)
        {
            StateType = eFSMStateType.ROAMING;
        }

        public override void Enter()
        {
            if (!_brain.PvPMode)
                _brain.AggroRange = 2000;

            base.Enter();
        }

        public override void Exit()
        {
            base.Exit();
        }

        public override void Think()
        {
            if (_brain.PreventCombat)
                return;

            if (!_brain.HasAggro)
            {
                if (_brain.Body.IsSitting && !_brain.CheckStats(75))
                    _brain.MimicBody.Sit(false);

                delayRoam = _brain.CheckDelayRoam();

                if (delayRoam && _brain.Body.IsDestinationValid)
                {
                    _brain.Body.StopMoving();
                }
                else if (!delayRoam && !_brain.Body.IsSitting && !_brain.Body.IsMoving && !_brain.Body.movementComponent.HasActiveResetHeadingAction)
                {
                    if (!_nextRoamingTickSet)
                    {
                        _nextRoamingTickSet = true;
                        _nextRoamingTick += Util.Random(MinCooldown, MaxCooldown) * 1000;
                        _brain.Body.SpawnPoint = new Point3D(_brain.Body.X, _brain.Body.Y, _brain.Body.Z);
                    }

                    if (ServiceUtils.ShouldTickAdjust(ref _nextRoamingTick))
                    {
                        // We're not updating `_nextRoamingTick` here because we want it to be set after the NPC stopped moving.
                        _nextRoamingTickSet = false;
                        _brain.Body.Roam(Speed);
                    }
                }
            }

            if (!_brain.PvPMode && delayRoam)
                return;

            if (_brain.CheckProximityAggro(_brain.AggroRange))
            {
                _brain.FSM.SetCurrentState(eFSMStateType.AGGRO);
                return;
            }

            base.Think();
        }
    }

    public class MimicState_Camp : MimicState
    {
        public int AggroRange = 0;

        public MimicState_Camp(MimicBrain brain) : base(brain)
        {
            StateType = eFSMStateType.CAMP;
        }

        public override void Enter()
        {
            if (_brain.Body.Group?.MimicGroup.CampPoint == null || !_brain.Body.IsWithinRadius(_brain.Body.Group?.MimicGroup.CampPoint, 1500))
            {
                _brain.FSM.SetCurrentState(eFSMStateType.WAKING_UP);
                return;
            }

            int randomX = _brain.Body.CurrentRegion.IsDungeon ? Util.Random(-50, 50) : Util.Random(-100, 100);
            int randomY = _brain.Body.CurrentRegion.IsDungeon ? Util.Random(-50, 50) : Util.Random(-100, 100);

            _brain.Body.SpawnPoint = new Point3D(_brain.Body.Group.MimicGroup.CampPoint);
            _brain.Body.SpawnPoint.X += randomX;
            _brain.Body.SpawnPoint.Y += randomY;

            _brain.AggroRange = _brain.Body.CurrentRegion.IsDungeon ? 250 : 550;

            if (AggroRange != 0)
                _brain.AggroRange = AggroRange;

            _brain.ClearAggroList();
            _brain.Body.ReturnToSpawnPoint(_brain.Body.MaxSpeed);
            _brain.IsPulling = false;
            _brain.PvPMode = false;

            base.Enter();
        }

        public override void Think()
        {
            if (!_brain.IsPulling && _brain.Body.IsDestinationValid)
                return;

            if (_brain.IsMainPuller)
                _brain.CheckPuller();

            if (_brain.IsMainCC)
                _brain.CheckMainCC();

            if (!_brain.IsPulling)
            {
                if (_brain.CheckProximityAggro(_brain.AggroRange))
                {
                    _brain.FSM.SetCurrentState(eFSMStateType.AGGRO);
                    return;
                }
            }

            if (!_brain.Body.IsMoving && !_brain.Body.InCombat)
            {
                if (!_brain.CheckSpells(MimicBrain.eCheckSpellType.Defensive))
                    _brain.MimicBody.Sit(_brain.CheckStats(75));
            }

            base.Think();
        }
    }

    public class MimicState_ReturnToSpawn : MimicState
    {
        public MimicState_ReturnToSpawn(MimicBrain brain) : base(brain)
        {
            StateType = eFSMStateType.RETURN_TO_SPAWN;
        }

        public override void Enter()
        {
            if (_brain.Body.WasStealthed)
                _brain.Body.Flags |= GameNPC.eFlags.STEALTH;

            _brain.ClearAggroList();
            _brain.Body.ReturnToSpawnPoint(GamePlayer.PLAYER_BASE_SPEED);
            base.Enter();
        }

        public override void Think()
        {
            if (!_brain.Body.IsNearSpawn &&
                (!_brain.HasAggro || !_brain.Body.IsEngaging) &&
                (!_brain.Body.IsReturningToSpawnPoint) &&
                _brain.Body.CurrentSpeed == 0)
            {
                _brain.FSM.SetCurrentState(eFSMStateType.WAKING_UP);
                _brain.Body.TurnTo(_brain.Body.SpawnHeading);
                return;
            }

            if (_brain.Body.IsNearSpawn)
            {
                _brain.FSM.SetCurrentState(eFSMStateType.WAKING_UP);
                _brain.Body.TurnTo(_brain.Body.SpawnHeading);
                return;
            }

            if (!_brain.PreventCombat)
            {
                if (_brain.CheckProximityAggro(_brain.AggroRange))
                {
                    if (_brain.Body.Group != null && _brain.Body.Group.MimicGroup.CampPoint != null)
                        _brain.FSM.SetCurrentState(eFSMStateType.CAMP);

                    _brain.FSM.SetCurrentState(eFSMStateType.AGGRO);
                    return;
                }
            }

            base.Think();
        }
    }

    public class MimicState_Patrolling : MimicState
    {
        public MimicState_Patrolling(MimicBrain brain) : base(brain)
        {
            StateType = eFSMStateType.PATROLLING;
        }

        public override void Enter()
        {
            _brain.Body.MoveOnPath(_brain.Body.MaxSpeed);
            _brain.ClearAggroList();
            base.Enter();
        }

        public override void Think()
        {
            if (!_brain.PreventCombat)
            {
                if (_brain.CheckProximityAggro(_brain.AggroRange))
                {
                    _brain.FSM.SetCurrentState(eFSMStateType.AGGRO);
                    return;
                }
            }

            base.Think();
        }
    }

    public class MimicState_Duel : MimicState
    {
        public MimicState_Duel(MimicBrain brain) : base(brain)
        {
            StateType = eFSMStateType.DUEL;
        }

        public override void Enter()
        {
            _brain.ClearAggroList();

            _brain.MimicBody.IsDuelReady = false;
            _brain.Body.IsSitting = false;
            _brain.AggroLevel = 100;
            _brain.PvPMode = true;
            _brain.AggroRange = 3600;
            _brain.Body.StopMoving();

            base.Enter();
        }

        public override void Think()
        {
            if (!_brain.CheckSpells(MimicBrain.eCheckSpellType.Defensive))
                _brain.MimicBody.IsDuelReady = true;

            if (_brain.MimicBody.DuelPartner != null && _brain.MimicBody.DuelPartner is IGamePlayer gPlayer)
            {
                if (gPlayer.IsDuelReady)
                {
                    _brain.CheckProximityAggro(_brain.AggroRange);
                    _brain.AttackMostWanted();
                }
            }
        }
    }

    public class MimicState_Dead : MimicState
    {
        public MimicState_Dead(MimicBrain brain) : base(brain)
        {
            StateType = eFSMStateType.DEAD;
        }

        public override void Enter()
        {
            _brain.ClearAggroList();
            base.Enter();
        }

        public override void Think()
        {
            _brain.FSM.SetCurrentState(eFSMStateType.WAKING_UP);
            base.Think();
        }
    }
}