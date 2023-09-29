﻿using System;
using System.Reflection;
using DOL.GS;
using DOL.GS.Scripts;
using DOL.Database;
using log4net;
using DOL.GS.Realm;
using System.Collections.Generic;
using DOL.GS.PlayerClass;

namespace DOL.GS.Scripts
{
	public class MimicThane : MimicNPC
	{
		private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

		public MimicThane(byte level) : base(new ClassThane(), level)
		{
			MimicSpec = new ThaneSpec();

			DistributeSkillPoints();
            MimicEquipment.SetMeleeWeapon(this, MimicSpec.WeaponTypeOne, eHand.twoHand);
            MimicEquipment.SetMeleeWeapon(this, MimicSpec.WeaponTypeOne, eHand.oneHand);
			MimicEquipment.SetShield(this, 2);
			MimicEquipment.SetArmor(this, eObjectType.Chain);
			MimicEquipment.SetJewelry(this);
            RefreshItemBonuses();
            SwitchWeapon(eActiveWeaponSlot.Standard);
			RefreshSpecDependantSkills(false);
			SetSpells();
            IsCloakHoodUp = Util.RandomBool();
        }
	}

	public class ThaneSpec : MimicSpec
	{
		public ThaneSpec()
		{
            SpecName = "ThaneSpec";

            int randBaseWeap = Util.Random(2);

            switch (randBaseWeap)
            {
                case 0: WeaponTypeOne = "Sword"; break;
                case 1: WeaponTypeOne = "Axe"; break;
                case 2: WeaponTypeOne = "Hammer"; break;
            }

            int randVariance = Util.Random(3);

			switch (randVariance)
			{
				case 0:
				case 1:
                Add(WeaponTypeOne, 39, 0.8f);
                Add("Stormcalling", 50, 1.0f);
                Add("Shields", 42, 0.5f);
                Add("Parry", 6, 0.0f);
				break;

                case 2:
                Add(WeaponTypeOne, 44, 0.8f);
                Add("Stormcalling", 48, 1.0f);
                Add("Shields", 35, 0.5f);
                Add("Parry", 18, 0.0f);
                break;

                case 3:
                Add(WeaponTypeOne, 50, 0.8f);
                Add("Stormcalling", 50, 1.0f);
                Add("Parry", 28, 0.1f);
                break;
            }
        }
	}
}