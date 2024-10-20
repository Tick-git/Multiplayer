using RimWorld;
using Verse;

namespace Multiplayer.Client.Factions
{
    internal class Page_ConfigureStartingPawns_Multifaction : Page_ConfigureStartingPawns
    {
        public override void PostClose()
        {
            Current.Game.InitData.playerFaction = null;
        }

        public override void PreOpen()
        {
            base.PreOpen();
            CreatePawnsForConfigurePage();
        }

        public static void CreatePawnsForConfigurePage()
        {
            var scenario = Find.Scenario;
            var prevProgramState = Current.programStateInt;
            Current.Game.InitData.playerFaction = GetTemporaryScenarioFactionForPawnConfigurePage(scenario);

            // Old Comment: Set ProgramState.Entry so that InInterface is false
            // TODO: Why is this necessary?
            // TODO: feel like there is a better way to do this
            Current.programStateInt = ProgramState.Entry;

            try
            {
                scenario.PostIdeoChosen();
            }
            finally
            {
                Current.programStateInt = prevProgramState;
            }
        }

        private static Faction GetTemporaryScenarioFactionForPawnConfigurePage(Scenario scenario)
        {
            return FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(scenario.playerFaction.factionDef));
        }
    }
}
