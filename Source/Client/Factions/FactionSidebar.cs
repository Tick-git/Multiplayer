using System.Collections.Generic;
using System.Linq;
using System.Text;
using Multiplayer.API;
using Multiplayer.Client.Factions;
using Multiplayer.Client.Util;
using Multiplayer.Common;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using UnityEngine.SceneManagement;
using Verse;

namespace Multiplayer.Client;

public static class FactionSidebar
{
    private static ScenarioDef chosenScenario = ScenarioDefOf.Crashlanded;
    private static string newFactionName;
    private static Vector2 scroll;

    public static void DrawFactionSidebar(Rect rect)
    {
        using var _ = MpStyle.Set(GameFont.Small);

        if (!Layouter.BeginArea(rect))
            return;

        Layouter.BeginScroll(ref scroll, spacing: 0f);

        using (MpStyle.Set(TextAnchor.MiddleLeft))
        using (MpStyle.Set(GameFont.Medium))
            Label("Create faction");

        Layouter.Rect(0, 2);

        DrawFactionCreator();

        Layouter.Rect(0, 12);

        using (MpStyle.Set(Color.gray))
            Widgets.DrawLineHorizontal(Layouter.LastRect().x, Layouter.LastRect().yMax, rect.width);

        Layouter.Rect(0, 12);

        using (MpStyle.Set(TextAnchor.MiddleLeft))
        using (MpStyle.Set(GameFont.Medium))
            Label("Join faction");

        Layouter.Rect(0, 7);

        DrawFactionChooser();

        Layouter.EndScroll();
        Layouter.EndArea();
    }

    private static void DrawFactionCreator()
    {
        DrawScenarioChooser();

        Layouter.Rect(0, 2);

        newFactionName = Widgets.TextField(Layouter.Rect(130, 24), newFactionName);

        Layouter.Rect(0, 7);

        if (Button("Settle new faction", 130, 30))
        {
            var tileError = new StringBuilder();

            if (newFactionName.NullOrEmpty())
                Messages.Message("The faction name can't be empty.", MessageTypeDefOf.RejectInput, historical: false);
            else if (Find.FactionManager.AllFactions.Any(f => f.Name == newFactionName))
            {
                Messages.Message("The faction name is already taken", MessageTypeDefOf.RejectInput, historical: false);
            }
            else if (Event.current.button == 1)
            {
                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>()
                {
                    new(
                        "Dev: create faction (no base)", () => DoCreateFaction(new ChooseIdeoInfo(null, null, null), false)
                    )
                }));
            }
            else if (Find.WorldInterface.SelectedTile < 0)
                Messages.Message("MustSelectStartingSite".TranslateWithBackup("MustSelectLandingSite"), MessageTypeDefOf.RejectInput, historical: false);
            else if (!TileFinder.IsValidTileForNewSettlement(Find.WorldInterface.SelectedTile, tileError))
                Messages.Message(tileError.ToString(), MessageTypeDefOf.RejectInput, historical: false);
            else
            {
                PreparePawnsForCharacterCreationPage();

                var pages = new List<Page>();
                Page_ChooseIdeo_Multifaction chooseIdeoPage = null;

                if (ModsConfig.IdeologyActive && !Find.IdeoManager.classicMode)
                    pages.Add(chooseIdeoPage = new Page_ChooseIdeo_Multifaction());

                pages.Add(new Page_ConfigureStartingPawns
                {
                    nextAct = () =>
                    {
                        DoCreateFaction(
                            new ChooseIdeoInfo(
                                chooseIdeoPage?.pageChooseIdeo.selectedIdeo,
                                chooseIdeoPage?.pageChooseIdeo.selectedStructure,
                                chooseIdeoPage?.pageChooseIdeo.selectedStyles
                            ),
                            true
                        );
                    }
                });

                var page = PageUtility.StitchedPages(pages);
                Find.WindowStack.Add(page);
            }
        }
    }

    
    private static void DrawScenarioChooser()
    {
        // Scenario chooser is disabled if Royalty, Ideology or Anomaly is active - because not tested
        if (ModsConfig.RoyaltyActive || ModsConfig.IdeologyActive || ModsConfig.AnomalyActive)
        {
            chosenScenario = ScenarioDefOf.Crashlanded;
            Label($"Choosing starting scenario is only possible with Core or Biotech");
            return;
        }

        Label($"Scenario: {chosenScenario?.label ?? Find.Scenario.name}");

        if (Mouse.IsOver(Layouter.LastRect()))
            Widgets.DrawAltRect(Layouter.LastRect());

        if (Widgets.ButtonInvisible(Layouter.LastRect()))
            OpenScenarioChooser();
    }

    private static void OpenScenarioChooser()
    {
        Find.WindowStack.Add(new FloatMenu(
            DefDatabase<ScenarioDef>.AllDefs.
                Except(ScenarioDefOf.Tutorial).
                Select(s =>
                {
                    return new FloatMenuOption(s.label, () =>
                    {
                        chosenScenario = s;                        
                    });
                }).
                ToList()));
    }

    private static void PreparePawnsForCharacterCreationPage()
    {
        var scenario = chosenScenario?.scenario ?? Current.Game.Scenario;
        var prevState = Current.programStateInt;

        Current.programStateInt = ProgramState.Entry; // Set ProgramState.Entry so that InInterface is false     
        Current.Game.Scenario = scenario;

        Current.Game.InitData = new GameInitData
        {
            startedFromEntry = true,
            playerFaction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(scenario.playerFaction.factionDef)),
            gameToLoad = "dummy" // Prevent special calculation path in GenTicks.TicksAbs
        };

        try
        { 
            scenario.PostIdeoChosen();
        }
        finally
        {
            Current.programStateInt = prevState;
        }
    }

    // MOVE TO UTIL class OR STH (if present)
    

    private static void DoCreateFaction(ChooseIdeoInfo chooseIdeoInfo, bool generateMap)
    {
        int playerId = Multiplayer.session.playerId;
        var prevState = Current.programStateInt;

        Current.Game.InitData.playerFaction = null;
        Current.programStateInt = ProgramState.Playing; // This is to force a sync

        try
        {
            if (Current.Game.InitData?.startingAndOptionalPawns is { } pawns)
                for (int i = 0; i < Find.GameInitData.startingPawnCount; i++)
                {
                    FactionCreator.SendPawn(playerId, pawns[i]);
                }

            FactionCreator.CreateFaction(
                playerId,
                newFactionName,
                Find.WorldInterface.SelectedTile,
                chosenScenario,
                chooseIdeoInfo,
                generateMap
            );
        }
        finally
        {
            Current.programStateInt = prevState;
        }
    }

    private static void DrawFactionChooser()
    {
        int i = 0;

        foreach (var playerFaction in Find.FactionManager.AllFactions.Where(f => f.def == FactionDefOf.PlayerColony || f.def == FactionDefOf.PlayerTribe))
        {
            if (playerFaction.Name == "Spectator") continue;

            Layouter.BeginHorizontal();
            if (i % 2 == 0)
                Widgets.DrawAltRect(Layouter.GroupRect());

            using (MpStyle.Set(TextAnchor.MiddleCenter))
                Label(playerFaction.Name, true);

            Layouter.FlexibleWidth();
            if (Button("Join", 70))
            {
                var factionHome = Find.Maps.FirstOrDefault(m => m.ParentFaction == playerFaction);
                if (factionHome != null)
                    Current.Game.CurrentMap = factionHome;

                // todo setting faction of self
                Multiplayer.Client.Send(
                    Packets.Client_SetFaction,
                    Multiplayer.session.playerId,
                    playerFaction.loadID
                );
            }
            Layouter.EndHorizontal();
            i++;
        }
    }

    public static void Label(string text, bool inheritHeight = false)
    {
        GUI.Label(inheritHeight ? Layouter.FlexibleWidth() : Layouter.ContentRect(text), text, Text.CurFontStyle);
    }

    public static bool Button(string text, float width, float height = 35f)
    {
        return Widgets.ButtonText(Layouter.Rect(width, height), text);
    }
}

public record ChooseIdeoInfo(
    IdeoPresetDef SelectedIdeo,
    MemeDef SelectedStructure,
    List<StyleCategoryDef> SelectedStyles
) : ISyncSimple;
