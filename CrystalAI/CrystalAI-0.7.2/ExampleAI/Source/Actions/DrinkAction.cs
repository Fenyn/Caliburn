﻿// GPL v3 License
// 
// Copyright (c) 2016-2017 Bismur Studios Ltd.
// Copyright (c) 2016-2017 Ioannis Giagkiozis
// 
// DrinkAction.cs is part of Crystal AI.
//  
// Crystal AI is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//  
// Crystal AI is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Crystal AI.  If not, see <http://www.gnu.org/licenses/>.
using Crystal;


namespace ExampleAI {

  public class DrinkAction : ActionBase<CharacterContext> {
    public static readonly string Name = "Drink";

    public override IAction Clone() {
      return new DrinkAction(this);
    }

    protected override void OnExecute(CharacterContext context) {
      context.Character.Report(Name);
      context.Bladder += 25f;
      context.Thirst -= 90f;
      context.Wealth -= 10f;
      EndInSuccess(context);
    }

    protected override void OnUpdate(CharacterContext context) {
    }

    public DrinkAction() {
    }

    DrinkAction(DrinkAction other) : base(other) {
    }

    public DrinkAction(IActionCollection collection) : base(Name, collection) {
    }
  }

}