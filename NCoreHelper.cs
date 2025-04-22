using Gecko.NCore.Client;
using Gecko.NCore.Client.ObjectModel.V3.No;
using Gecko.NCore.Client.Functions.V2;
using Gecko.NCore.Client.Querying;
using System.Text.RegularExpressions;
using System.Data;
using Serilog;
using Microsoft.Extensions.Logging;
using System.Xml;



namespace NCoreClientSample
{
    public static class NCoreHelper
    {
        public static Rolle HentRolle(IEphorteContext context, int rolleId)
        {
            try
            {
                var query = from r in context.Query<Rolle>() where r.Id == rolleId select r;
                return query.First();
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("RolleId {0} ikke funnet!\nDetaljer: {1}", rolleId, ex.Message));
            }
        }

        public static PersonRolle HentPersonrolle(IEphorteContext context, int personRolleId)
        {
            try
            {
                var query = from pr in context.Query<PersonRolle>() where pr.Id == personRolleId select pr;
                return query.First();
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("PersonRolleId {0} ikke funnet!\nDetaljer: {1}", personRolleId, ex.Message));
            }
        }

        public static JournalEnhet HentJournalEnhetForPersonrolle(IEphorteContext context, int personRolleId)
        {
            try
            {
                PersonRolle personRolle = HentPersonrolle(context, personRolleId);
                var query = from j in context.Query<JournalEnhet>() where j.Id == personRolle.JournalEnhetId select j;
                return query.First();
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("JournalEnhet ikke funnet for personRolleId {0} !\nDetaljer: {1}", personRolleId, ex.Message));
            }
        }

        public static Arkivdel HentArkivdelForPersonrolle(IEphorteContext context, int personRolleId)
        {
            try
            {
                PersonRolle personRolle = HentPersonrolle(context, personRolleId);
                var query = from a in context.Query<Arkivdel>() where a.Id == personRolle.ArkivdelId select a;
                return query.First();
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Arkivdel ikke funnet for personRolleId {0} !\nDetaljer: {1}", personRolleId, ex.Message));
            }
        }

        public static AdministrativEnhet HentAdministrativEnhet(IEphorteContext context, int administrativEnhetId)
        {
            try
            {
                var query = from a in context.Query<AdministrativEnhet>() where a.Id == administrativEnhetId select a;
                return query.First();
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("AdministrativEnhet med id {0} ikke funnet!\nDetaljer: {1}", administrativEnhetId, ex.Message));
            }
        }

        public static NCoreUser HentInfoOmBruker(IEphorteContext context, string brukernavn)
        {
            Personnavn personnavn = null;
            try
            {
                personnavn = HentPersonnavnForBruker(context, brukernavn);

                object functionResult = context.Functions.Execute("HentRolleHandler", new object[] { personnavn.PersonId }); //Input: peId
                if (functionResult == null) throw new Exception(string.Format("Ingen personrolle definert for PersonId {0} i ePhorte!", personnavn.PersonId));

                string[] personRolleRows = functionResult.ToString().Split(';');
                foreach (string personRolleRow in personRolleRows)
                {
                    if (string.IsNullOrEmpty(personRolleRow)) throw new Exception(string.Format("Bruker {0} har ingen aktiv rolle!", brukernavn));
                    string[] personRolleColumns = personRolleRow.Split(',');
                    string personrolleId = personRolleColumns[0];
                    string rolleId = personRolleColumns[1];
                    string stdRolle = personRolleColumns[2];
                    string personrolleTittel = personRolleColumns[3];
                    string administrativEnhetId = personRolleColumns[4];
                    string personrolleTittel2 = personRolleColumns[5];

                    if (stdRolle == "-1")
                    {
                        var brukerInfo = new NCoreUser(personnavn, Convert.ToInt32(administrativEnhetId), Convert.ToInt32(personrolleId), Convert.ToInt32(rolleId));
                        return brukerInfo;
                    }

                }
                throw new Exception(string.Format("Feil i HentInfoOmBruker. Ingen standard rolle definert for PersonId {0}", personnavn != null ? personnavn.PersonId.Value.ToString() : "<ingen personid>"));
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Feil i HentInfoOmBruker. Brukernavn: {0}\nDetaljer: {1}", brukernavn, ex.Message));
            }
        }



        public static Personnavn HentPersonnavnForBruker(IEphorteContext context, string brukernavn)
        {
            try
            {
                var personQuery = from c in context.Query<Person>().Include(x => x.AktivtNavn).Include(x => x.AktivtNavn.Person)
                                  where c.Brukernavn == brukernavn
                                  select c;
                var person = personQuery.FirstOrDefault();

                if (person == null) throw new Exception(string.Format("Person.Brukernavn {0} ble ikke funnet i ePhorte Person-tabell! (PERSON.PE_BRUKERID)", brukernavn));
                if (person.AktivtNavn == null) throw new Exception(string.Format("Personnavn (Person.AktivtNavn) for Person.Brukernavn {0} ble ikke funnet i ePhorte!", brukernavn));

                return person.AktivtNavn;
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Feil ved identifisering av bruker {0}\nSjekk at integrasjonens systembruker finnes i databasen.\nDetaljer: {1}", brukernavn, ex.Message));
            }
        }

        public static Person HentPersonForEpostadresse(IEphorteContext context, string epostAdr)
        {
            try
            {
                var personAdresse = context.Query<PersonAdresse>().Include(pa => pa.Person).Include(pa => pa.Person.AktivtNavn).Include(pa => pa.Adresse)
                                    .Where(pa => pa.Adresse.EPostAdresse == epostAdr).FirstOrDefault();

                return personAdresse?.Person;
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Feil ved identifisering av bruker fra epostadresse {0}\nDetaljer: {1}", epostAdr, ex.Message));
            }
        }

        private static string ByttUtHvertOrdMed5Stjerner(string tekst)
        {
            string skjermetTekst = "";

            //MatchCollection ordListe = System.Text.RegularExpressions.Regex.Matches(tekst, @"[\S]+"); //teller ord

            foreach (string ord in tekst.Split(' '))
            {
                if (ord.Trim() == "") continue;

                skjermetTekst += " *****";
            }
            return skjermetTekst.Trim();
        }

        private static string ByttUtHvertOrdMed5Kryss(string tekst)
        {
            string returnTekst = "";

            foreach (string ord in tekst.Split(' '))
            {
                if (ord.Trim() == "") continue;

                returnTekst += " #####";
            }

            if (returnTekst.EndsWith("#")) returnTekst = returnTekst.Substring(0, returnTekst.Length - 1) + "_";

            return returnTekst.Trim();
        }

        private static string ByttUtHvertOrdMed5Plusstegn(string tekst)
        {
            string returnTekst = "";

            foreach (string ord in tekst.Split(' '))
            {
                if (ord.Trim() == "") continue;

                returnTekst += " +++++";
            }

            if (returnTekst.EndsWith("+")) returnTekst = returnTekst.Substring(0, returnTekst.Length - 1) + "_";

            return returnTekst.Trim();

        }

        public enum Skjerming
        {
            Ingen = 0,
            Skjerming,
            Markering,
            SkjermingOgMarkering,
        }




        /// <summary>
        /// Skjermer alle ord mellom to @-tegn. Hvis det kun er én @ vil alt etter denne bli skjermet.
        /// Markerer alle ord mellom to #-tegn. Hvis det kun er én # vil alt etter denne bli markert.
        /// Skjermer og markerer alle ord mellom to @#-tegn. Hvis det kun er én @# vil alt etter denne bli skjermet og markert.
        /// 
        /// Regel: Hvert ord som skjermes erstattes av 5 stjerner (*).
        /// Eksempel:
        /// Tittel: Søknad om utsettelse av betaling
        /// Sak.Sakstittel : Søknad om utsettelse av betaling
        /// Sak.SkjermetSakstittel : Søknad om ***** ***** *****
        /// Sak.SakstittelPersonnavn: Søknad om ***** ***** *****
        /// </summary>
        public static void SkjermingOgMarkeringAvTekst(string tittel, Object obj)
        {
            if (String.IsNullOrWhiteSpace(tittel)) return;

            #region Workaround to avoid error: Database feltene for markerte navn og uskjermet tekst inneholder forskjellig antall ord. Skriv inn en tittel på nytt og velg lagre.
            tittel = Regex.Replace(tittel, @"\s+", " ");
            tittel = tittel.Trim();
            #endregion

            string Tekst = "";
            string SkjermetTekst = "";
            string MarkertTekst = "";
            string SkjermetOgMarkertTekst = "";

            Skjerming skjerming = Skjerming.Ingen;

            int countOfBeggeTegn = 0, n = 0;
            while ((n = tittel.IndexOf("@#", n, StringComparison.InvariantCulture)) != -1)
            {
                n += "@#".Length;
                ++countOfBeggeTegn;
            }
            int countOfSkjermetegn = tittel.Count(x => x == '@');
            int countOfMarkeringstegn = tittel.Count(x => x == '#');

            if (countOfBeggeTegn > 0)
            {
                if (countOfBeggeTegn > 2) throw new Exception($"Kan ikke legge på skjerming og markering så lenge teksten inneholder flere enn 2 innslag av 2-tegnskominasjonen @#\nTekst: {tittel}");

                skjerming = Skjerming.SkjermingOgMarkering;

                string[] stringSeparators = { "@#" };
                string[] parts = tittel.Split(stringSeparators, StringSplitOptions.None);
                for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();

                #region skjerming
                if (parts.Length == 3) SkjermetTekst = parts[0] + " " + ByttUtHvertOrdMed5Stjerner(parts[1]) + " " + parts[2];
                else if (parts.Length == 2) SkjermetTekst = parts[0] + " " + ByttUtHvertOrdMed5Stjerner(parts[1]);
                else SkjermetTekst = tittel;
                #endregion

                #region markering
                if (parts.Length == 3) MarkertTekst = parts[0] + " " + ByttUtHvertOrdMed5Kryss(parts[1]) + " " + parts[2];
                else if (parts.Length == 2) MarkertTekst = parts[0] + " " + ByttUtHvertOrdMed5Kryss(parts[1]);
                else MarkertTekst = tittel;
                #endregion

                #region skjermet og markert
                if (parts.Length == 3) SkjermetOgMarkertTekst = parts[0] + " " + ByttUtHvertOrdMed5Plusstegn(parts[1]) + " " + parts[2];
                else if (parts.Length == 2) SkjermetOgMarkertTekst = parts[0] + " " + ByttUtHvertOrdMed5Plusstegn(parts[1]);
                else MarkertTekst = tittel;
                #endregion

            }
            else if (countOfSkjermetegn > 0)
            {
                if (countOfSkjermetegn > 2) throw new Exception(string.Format("Kan ikke legge på skjerming så lenge teksten inneholder flere enn 2 @-tegn!\nTekst: {0}", tittel));

                skjerming = Skjerming.Skjerming;

                string[] parts = tittel.Split('@');
                for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();

                if (parts.Length == 3) SkjermetTekst = parts[0] + " " + ByttUtHvertOrdMed5Stjerner(parts[1]) + " " + parts[2];
                else if (parts.Length == 2) SkjermetTekst = parts[0] + " " + ByttUtHvertOrdMed5Stjerner(parts[1]);
                else SkjermetTekst = tittel;

            }
            else if (countOfMarkeringstegn > 0)
            {
                if (countOfMarkeringstegn > 2) throw new Exception(string.Format("Kan ikke legge på markering så lenge teksten inneholder flere enn 2 #-tegn!\nTekst: {0}", tittel));

                skjerming = Skjerming.Markering;

                string[] parts = tittel.Split('#');
                for (int i = 0; i < parts.Length; i++) parts[i] = parts[i].Trim();

                if (parts.Length == 3) MarkertTekst = parts[0] + " " + ByttUtHvertOrdMed5Kryss(parts[1]) + " " + parts[2];
                else if (parts.Length == 2) MarkertTekst = parts[0] + " " + ByttUtHvertOrdMed5Kryss(parts[1]);
                else MarkertTekst = tittel;
            }
            Tekst = Regex.Replace(Regex.Replace(tittel, @"\s*[@#]\s*", " "), @"\s+", " ");

            if (obj is Sak sak)
            {
                if (skjerming == Skjerming.SkjermingOgMarkering)
                {
                    sak.Tittel = Tekst;
                    sak.TittelOffentlig = SkjermetTekst;
                    sak.TittelPersonnavn = SkjermetOgMarkertTekst;
                }
                else if (skjerming == Skjerming.Skjerming)
                {
                    sak.Tittel = Tekst;
                    sak.TittelOffentlig = SkjermetTekst;
                    sak.TittelPersonnavn = SkjermetTekst;
                }
                else if (skjerming == Skjerming.Markering)
                {
                    sak.Tittel = Tekst;
                    sak.TittelOffentlig = Tekst;
                    sak.TittelPersonnavn = MarkertTekst;
                }
                else
                {
                    sak.Tittel = Tekst;
                    sak.TittelOffentlig = Tekst;
                    sak.TittelPersonnavn = Tekst;
                }
            }
            else if (obj is Journalpost jp)
            {
                if (skjerming == Skjerming.SkjermingOgMarkering)
                {
                    jp.Innholdsbeskrivelse = Tekst;
                    jp.InnholdsbeskrivelseOffentlig = SkjermetTekst;
                    jp.InnholdsbeskrivelsePersonnavn = SkjermetOgMarkertTekst;
                }
                else if (skjerming == Skjerming.Skjerming)
                {
                    jp.Innholdsbeskrivelse = Tekst;
                    jp.InnholdsbeskrivelseOffentlig = SkjermetTekst;
                    jp.InnholdsbeskrivelsePersonnavn = SkjermetTekst;
                }
                else if (skjerming == Skjerming.Markering)
                {
                    jp.Innholdsbeskrivelse = Tekst;
                    jp.InnholdsbeskrivelseOffentlig = Tekst;
                    jp.InnholdsbeskrivelsePersonnavn = MarkertTekst;
                }
                else
                {
                    jp.Innholdsbeskrivelse = Tekst;
                    jp.InnholdsbeskrivelseOffentlig = Tekst;
                    jp.InnholdsbeskrivelsePersonnavn = Tekst;
                }
            }
        }


        public static void SkrivFunksjonerTilFil(Microsoft.Extensions.Logging.ILogger logger, IEphorteContext context)
        {
            try
            {
                logger.LogDebug("\n\nFunksjoner i Gecko.NCore.Client.Functions:");

                string logTekst = "";

                foreach (Gecko.NCore.Client.Functions.FunctionDescriptor func in context.Functions.SupportedFunctions.Select(f => f))
                {
                    logTekst += string.Format("\n------------------------------------------------------");
                    logTekst += string.Format("\nFunksjon: {0}\n\t Paramtere:", func.Name);
                    foreach (var key in func.Parameters.Keys)
                        logTekst += string.Format("\n\t\t Parameter: {0}   (type: {1})", key, func.Parameters[key]);
                }

                logger.LogInformation(logTekst);
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Feil i SkrivFunksjonerTilFil. Detaljer: {0}", ex.Message));
            }
        }

        public static void AvskrivJournalpostDirekte(IEphorteContext context, string avskrivningsmaate, int journalpostId)
        {
            context.Functions.Execute("AvskrivJournalpostDirekte", new object[] { journalpostId, avskrivningsmaate, null });
        }

        public static void AvskrivOgAvsluttSak(IEphorteContext context, Sak sak)
        {
            try
            {
                context.Functions.Execute("AvskrivOgAvsluttSak", new object[] { sak.Id });
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Feil i AvskrivOgAvsluttSak. Gjelder sak {0}. Feil: {1}", FormatSaksRef(sak), ex.Message));
            }
        }

        public static string HentNCoreVersjon(IEphorteContext context)
        {
            try
            {
                return context.Functions.Execute("GetVersionString").ToString();
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Feil i HentNCoreVersjon. Detaljer: {0}", ex.Message));
            }
        }

        

        public static void FlyttJournalpost(IEphorteContext context, int journalpostId, int fraEphorteSakId, int tilEphorteSakId)
        {
            try
            {
                context.Functions.Execute("FlyttJournalpost", new object[] { journalpostId, fraEphorteSakId, tilEphorteSakId });
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Feil i FlyttJournalpost. Detaljer: {0}", ex.Message));
            }
        }


      
        public static string FormatSaksRef(Sak sak)
        {
            if (sak == null) return "";

            try
            {
                return string.Format("{0}/{1}", sak.Saksaar.HasValue ? sak.Saksaar.Value.ToString() : "", sak.Sekvensnummer.HasValue ? sak.Sekvensnummer.Value.ToString() : "");
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Feil i FormatSaksRef: {0}", ex.Message));
            }
        }

      

        public static string FormatJournalpostDokRef(Sak sak, Journalpost journalpost)
        {
            try
            {
                return string.Format("{0}/{1}-{2}", sak.Saksaar.Value, sak.Sekvensnummer.Value, journalpost.Dokumentnummer);
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Feil i FormatJournalpostDokRef: {0}", ex.Message));
            }
        }

        public static void SendJournalpostPaaGodkjenningsflyt(IEphorteContext context, Microsoft.Extensions.Logging.ILogger logger, Journalpost journalpost, string godkjennereEpostListe)
        {
            try
            {
                logger.LogDebug($"Henter brukere fra epost '{godkjennereEpostListe}'...");

                var emails = godkjennereEpostListe.Split(new char[] { ',',';' });
                string approversUnitAndPersonId = string.Empty;
                NCoreUser firstApprover = null;

                int count = 1;

                foreach (var email in emails)
                {
                    var person = HentPersonForEpostadresse(context, email);
                    var approver = HentInfoOmBruker(context, person.Brukernavn);

                    if (count == 1)
                        firstApprover = approver;

                    approversUnitAndPersonId += $"{approver.AdministrativEnhetId}:{approver.PersonnavnId};";

                    count++;
                }

                approversUnitAndPersonId = approversUnitAndPersonId.TrimEnd(';');

                object functionResult = context.Functions.Execute("NewActivitiesFromTemplate",
                   new object[]
                     {
                     -10, //templateId -10 for godkjenningsflyt
                     journalpost.Id, // journalpostId
                     1, // type
                     -10, //posisjon
                     true, //sammeNivaa
                     $"{approversUnitAndPersonId}" // ADM_ID:PN_ID (støtte for flere godkjennere med ";" i mellom
                     });

                long activityId = (long)functionResult;

                logger.LogDebug($"Oppdaterer journalstatus og angir første godkjenner");
                journalpost.JournalstatusId = "G"; //G = Til Godkjenning
                journalpost.GodkjentAvId = firstApprover.PersonnavnId;

                context.SaveChanges();
            }
            catch (Exception ex)
            {
                throw new Exception($"Feil i SendJournalpostPaaGodkjenningsflyt: {ex.Message}");
            }
        }

        

        public static FileData HentDokumentmal(IEphorteContext context, string malBetegnelse)
        {
            try
            {
                var template = context.Query<Dokumentmal>().Where(x => x.Betegnelse == malBetegnelse).FirstOrDefault();

                if (template == null)
                    throw new Exception($"Dokumentmal '{malBetegnelse}' finnes ikke");

                var templateData = context.Functions.Execute("GetDocTemplate", new object[] { template.Id, 0, "FILNAVN" });
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(templateData.ToString());
                XmlNode fileData = xmlDoc.SelectSingleNode("//FileData");
                var file = fileData.SelectSingleNode("FileName")?.InnerText;
                var fileType = fileData.SelectSingleNode("FileType")?.InnerText;
                var base64Content = fileData.SelectSingleNode("ByteArray")?.InnerText;

                if (string.IsNullOrEmpty(fileType))
                    fileType = template.Filetternavn;

                return new FileData { TemplateFileName = Path.GetFileName(file), FileType = fileType, TemplateFileContentBase64 = base64Content };
            }
            catch (Exception ex)
            {
                throw new Exception(string.Format("Feil i HentDokumentmal. Detaljer: {0}", ex.Message));
            }
        }

    }

    public class FileData
    {
        public string TemplateFileName = string.Empty;
        public string FileType = string.Empty;
        public string TemplateFileContentBase64 = string.Empty; 
    }
}
