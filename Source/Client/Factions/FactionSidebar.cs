using System.Collections.Generic;
using System.Linq;
using System.Text;
using Multiplayer.Client.Factions;
using Multiplayer.Client.Util;
using Multiplayer.Common;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Multiplayer.Client;

public static class FactionSidebar
{
    private static ScenarioDef chosenScenario = ScenarioDefOf.Crashlanded;
    private static string newFactionName;
    private static Vector2 scroll;

    public static void DrawFactionSidebar(Rect factionBarRect)
    {
        using var _ = MpStyle.Set(GameFont.Small);

        if (!Layouter.BeginArea(factionBarRect))
            return;

        Layouter.BeginScroll(ref scroll, spacing: 0f);

        DrawFactionCreator();

        DrawDividingLine(factionBarRect);   

        DrawFactionChooser();

        Layouter.EndScroll();
        Layouter.EndArea();
    }

    private static void DrawFactionCreator()
    {
        DrawFactionCreatorHeadline();

        DrawScenarioChooser();

        DrawFactionNameTextfield();

        if (Button("Settle new faction", 130, 30) && FactionCreationCanBeStarted())
        {
            OpenConfigurationPages();
        }
    }

    private static void OpenConfigurationPages()
    {
        var gameConfigurationPages = new List<Page>();
        Page_ChooseIdeo_Multifaction chooseIdeoPage = null;
        Page_ConfigureStartingPawns pawnConfigPage = null;

        BeginScenarioConfiguration(chosenScenario.scenario);

        chooseIdeoPage = GetIdeologyConfigurationPage();
        pawnConfigPage = new Page_ConfigureStartingPawns_Multifaction()
        {
            nextAct = () => DoCreateFaction(Page_ChooseIdeo_Multifaction.GetChooseIdeoInfoForIdeoPage(chooseIdeoPage), true)
        };

        if (chooseIdeoPage != null)
            gameConfigurationPages.Add(chooseIdeoPage);

        gameConfigurationPages.Add(pawnConfigPage);

        var combinedPages = PageUtility.StitchedPages(gameConfigurationPages);
        Find.WindowStack.Add(combinedPages);
    }

    private static void DoCreateFaction(ChooseIdeoInfo chooseIdeoInfo, bool generateMap)
    {
        int playerId = Multiplayer.session.playerId;
        var prevState = Current.programStateInt;
        List<Pawn> startingPawns = new List<Pawn>();
        FactionCreationData factionCreationData = new FactionCreationData();

        // OldComment: This is to force a sync
        // TODO: Make this clearer without a needed comment
        Current.programStateInt = ProgramState.Playing;

        try
        {
            if (Current.Game.InitData?.startingAndOptionalPawns is { } pawns)
                for (int i = 0; i < Find.GameInitData.startingPawnCount; i++)
                {
                    FactionCreator.SendPawn(playerId, pawns[i]);
                    startingPawns.Add(pawns[i]);
                }

            factionCreationData.factionName = newFactionName;
            factionCreationData.startingTile = Find.WorldInterface.SelectedTile;
            factionCreationData.scenarioDef = chosenScenario;
            factionCreationData.chooseIdeoInfo = chooseIdeoInfo;
            factionCreationData.generateMap = generateMap;
            factionCreationData.startingPossessions = GetStartingPossessions(startingPawns);

            FactionCreator.CreateFaction(playerId, factionCreationData);
        }
        finally
        {
            Current.programStateInt = prevState;
        }
    }

    private static void DrawFactionChooser()
    {
        using (MpStyle.Set(TextAnchor.MiddleLeft))
        using (MpStyle.Set(GameFont.Medium))
            Label("Join faction");

        Layouter.Rect(0, 7);

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

    private static void DrawFactionCreatorHeadline()
    {
        using (MpStyle.Set(TextAnchor.MiddleLeft))
        using (MpStyle.Set(GameFont.Medium))
            Label("Create faction");

        Layouter.Rect(0, 2);
    }

    private static void DrawFactionNameTextfield()
    {
        Layouter.Rect(0, 2);

        newFactionName = Widgets.TextField(Layouter.Rect(130, 24), newFactionName);

        Layouter.Rect(0, 7);
    }

    private static bool FactionCreationCanBeStarted()
    {
        var tileError = new StringBuilder();

        if (newFactionName.NullOrEmpty())
        {
            Messages.Message("The faction name can't be empty.", MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }
        else if (Find.FactionManager.AllFactions.Any(f => f.Name == newFactionName))
        {
            Messages.Message("The faction name is already taken", MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }
        else if (Event.current.button == 1)
        {
            Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>()
                {
                    new(
                        "Dev: create faction (no base)", () => DoCreateFaction(new ChooseIdeoInfo(null, null, null), false)
                    )
                }));

            return false;
        }
        else if (Find.WorldInterface.SelectedTile < 0)
        {
            Messages.Message("MustSelectStartingSite".TranslateWithBackup("MustSelectLandingSite"), MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }
        else if (!TileFinder.IsValidTileForNewSettlement(Find.WorldInterface.SelectedTile, tileError))
        {
            Messages.Message(tileError.ToString(), MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }

        return true;
    }

    private static Page_ChooseIdeo_Multifaction GetIdeologyConfigurationPage()
    {
        Page_ChooseIdeo_Multifaction chooseIdeoPage = null;

        if (ModsConfig.IdeologyActive && !Find.IdeoManager.classicMode)
            chooseIdeoPage = new Page_ChooseIdeo_Multifaction();

        return chooseIdeoPage;
    }

    private static void BeginScenarioConfiguration(Scenario scenario)
    {
        Current.Game.Scenario = scenario;

        Current.Game.InitData = new GameInitData
        {
            startedFromEntry = true,
            gameToLoad = FactionCreator.preventSpecialCalculationPathInGenTicksTicksAbs
        };
    }

    private static void DrawScenarioChooser()
    {
        // Scenario chooser is disabled if Royalty or Anomaly is active - because not tested
        if (ModsConfig.RoyaltyActive || ModsConfig.AnomalyActive)
        {
            chosenScenario = ScenarioDefOf.Crashlanded;
            Label($"Choosing starting scenario is only possible with Core, Biotech and Ideology");
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

    private static List<ThingDefCount> GetStartingPossessions(List<Pawn> startingPawns)
    {
        Dictionary<Pawn, List<ThingDefCount>> allPossessions = Find.GameInitData.startingPossessions;
        List<ThingDefCount> startingPossessions = new List<ThingDefCount>();

        foreach(Pawn pawn in startingPawns)
        {
            startingPossessions.AddRange(allPossessions[pawn]);
        }

        return startingPossessions;
    }

    private static void DrawDividingLine(Rect factionBarRect)
    {
        Layouter.Rect(0, 12);

        using (MpStyle.Set(Color.gray))
            Widgets.DrawLineHorizontal(Layouter.LastRect().x, Layouter.LastRect().yMax, factionBarRect.width);

        Layouter.Rect(0, 12);
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
