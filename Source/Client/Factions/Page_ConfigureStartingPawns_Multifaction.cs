using RimWorld;
using Verse;

namespace Multiplayer.Client.Factions
{
    internal class Page_ConfigureStartingPawns_Multifaction : Page_ConfigureStartingPawns
    {
        Faction _temporaryFactionForPawnCreation;
        Scenario _scenario;

        public override void PreOpen()
        {
            base.PreOpen();

            _scenario = Find.Scenario;

            GenerateTemporaryFaction();
            GeneratePawnsForConfigurePage();
        }

        public override void PostClose()
        {
            base.PostClose();

            CleanUpTemporaryFaction();
        }

        public override void DoNext()
        {
            CleanUpTemporaryFaction();

            base.DoNext();
        }

        private void CleanUpTemporaryFaction()
        {
            if (_temporaryFactionForPawnCreation == null) return;

            Find.FactionManager.Remove(_temporaryFactionForPawnCreation);
            Find.FactionManager.toRemove.Remove(_temporaryFactionForPawnCreation);

            Current.Game.InitData.playerFaction = null;
            _temporaryFactionForPawnCreation = null;
        }

        private void GenerateTemporaryFaction()
        {
            _temporaryFactionForPawnCreation = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(_scenario.playerFaction.factionDef));
            _temporaryFactionForPawnCreation.temporary = true;

            Find.FactionManager.Add(_temporaryFactionForPawnCreation);
        }

        private void GeneratePawnsForConfigurePage()
        {
            var prevProgramState = Current.programStateInt;

            // Old Comment: Set ProgramState.Entry so that InInterface is false
            // TODO: Why is this necessary?
            // TODO: feel like there is a better way to do this
            Current.programStateInt = ProgramState.Entry;
            Current.Game.InitData.playerFaction = _temporaryFactionForPawnCreation;

            try
            {
                GeneratingPawns();
            }
            finally
            {       
                Current.programStateInt = prevProgramState;
            }
        }

        private void GeneratingPawns()
        {
            _scenario.PostIdeoChosen();
        }
    }
}
