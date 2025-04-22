using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Gecko.NCore.Client;
using Gecko.NCore.Client.ObjectModel.V3.No;

namespace NCoreClientSample
{
    public class NCoreUser
    {
        public Personnavn Personnavn { get; internal set; }
        public int PersonnavnId { get; internal set; }
        public int AdministrativEnhetId { get; internal set; }
        public int StdPersonrolleId { get; internal set; }
        public int StdRolleId { get; internal set; }

        public NCoreUser(Personnavn personnavn, int admministrativEnhetId, int stdPersonrolleId, int stdRolleId)
        {
            this.Personnavn = personnavn;
            this.PersonnavnId = personnavn.Id;
            this.AdministrativEnhetId = admministrativEnhetId;
            this.StdPersonrolleId = stdPersonrolleId;
            this.StdRolleId = stdRolleId;
        }
        

        private AdministrativEnhet _administrativEnhet = null;
        public AdministrativEnhet HentAdministrativEnhet(IEphorteContext context)
        {
            if (_administrativEnhet != null) return _administrativEnhet;
            _administrativEnhet =  NCoreHelper.HentAdministrativEnhet(context, this.AdministrativEnhetId);
            return _administrativEnhet;
        }

        private Arkivdel _arkivdel = null;
        public Arkivdel HentArkivdel(IEphorteContext context)
        {
            if (_arkivdel != null) return _arkivdel;
            _arkivdel = NCoreHelper.HentArkivdelForPersonrolle(context, this.StdPersonrolleId);
            return _arkivdel;
        }

        private JournalEnhet _journalEnhet = null;
        public JournalEnhet HentJournalEnhet(IEphorteContext context)
        {
            if (_journalEnhet != null) return _journalEnhet;
            _journalEnhet = NCoreHelper.HentJournalEnhetForPersonrolle(context, this.StdPersonrolleId);
            return _journalEnhet;
        }

        private PersonRolle _personrolle = null;
        public PersonRolle HentPersonRolle(IEphorteContext context)
        {
            if (_personrolle != null) return _personrolle;
            _personrolle = NCoreHelper.HentPersonrolle(context, this.StdPersonrolleId);
            return _personrolle;
        }

        private Rolle _rolle = null;
        public Rolle HentRolle(IEphorteContext context)
        {
            if (_rolle != null) return _rolle;
            _rolle = NCoreHelper.HentRolle(context, this.StdRolleId);
            return _rolle;
        }
    }
}
