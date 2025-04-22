using Gecko.NCore.Client.Querying;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Xml;
using Gecko.NCore.Client.ObjectModel.V3.No;
using Serilog.Core;
using Gecko.NCore.Client;
using Gecko.NCore.Client.ObjectModel.V3.En;
using System;
//using Gecko.NCore.Client.ObjectModel.V3.En;


namespace NCoreClientSample
{
    internal class Program
    {
        static void Main(string[] args)
        {
            #region Setup Config and Services
            using IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureHostConfiguration(configHost =>
            {
                configHost.SetBasePath(Directory.GetCurrentDirectory());
                configHost.AddJsonFile("appsettings.json", true, true);
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<NCoreSettings>(context.Configuration.GetSection("NCoreSettings"));
                services.AddSingleton<NCoreFactory>();
            })
            .UseSerilog((context, configuration) =>
            {
                configuration.ReadFrom.Configuration(context.Configuration);
            })
            .Build();
            
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var nCoreFactory = host.Services.GetRequiredService<NCoreFactory>();
            #endregion

            using (var context = nCoreFactory.Create())
            {

                var person = NCoreHelper.HentPersonForEpostadresse(context, "demobruker@sikri.no");
                var ncoreBruker = NCoreHelper.HentInfoOmBruker(context, person.Brukernavn);

                #region Hent lese-/skrive logg
                var saksbehandler = context.Query<Personnavn>().Where(x => x.Initialer == "INNY").FirstOrDefault();
                var komplettLoggForSaksbehandler = context.Query<LoggInformasjon>().Where(x => x.RegistrertAvId == saksbehandler.Id.ToString()).OrderByDescending(x => x.Klokkeslett).ToList();
                //var aktuelleTabeller = komplettLoggForSaksbehandler.DistinctBy(x => x.DatabaseTabell).Select(x => x.DatabaseTabell).ToList();
                //var aktuellHogghendelseTyper = context.Query<LoggHendelse>().ToList();  

                var leseloggForSaksbehandler = komplettLoggForSaksbehandler.Where(x => x.HendelsesId == 4).ToList(); //4=Lest dokument
                foreach (var hendelse in leseloggForSaksbehandler)
                    logger.LogDebug($"Leselogg: {saksbehandler.Navn} har lest post {hendelse.Primærnøkkelfelt0} i tabell {hendelse.DatabaseTabell}");

                var skriveloggForSaksbehandler = komplettLoggForSaksbehandler.Where(x => x.HendelsesId == 2).ToList(); //2=Endret post
                foreach (var hendelse in skriveloggForSaksbehandler)
                    logger.LogDebug($"Skrivelogg: {saksbehandler.Navn} har endret post {hendelse.Primærnøkkelfelt0} i tabell {hendelse.DatabaseTabell}");
                #endregion


                #region Opprett saker
                for (var i = 0; i < 11; i++)
                {
                    var identifier = $"Klientsak-{i.ToString().PadLeft(3, '0')}";

                    var sak = context.Query<Sak>().Where(x => x.Tilleggsattributt1 == identifier).FirstOrDefault();
                    if (sak != null)
                        continue;

                    sak = new Sak
                    {
                        Arkivdel = ncoreBruker.HentArkivdel(context),
                        JournalEnhet = ncoreBruker.HentJournalEnhet(context),
                        AnsvarligEnhet = ncoreBruker.HentAdministrativEnhet(context),
                        AnsvarligPerson = ncoreBruker.Personnavn,
                        TilgangskodeId = "K",
                        MappetypeId = "KSA",
                        Tilleggsattributt1 = identifier,
                        SaksstatusId = "B",
                    };
                    NCoreHelper.SkjermingOgMarkeringAvTekst($"{identifier} for @Ola Normann@", sak);
                    context.Add(sak);

                    var sakspart = new Sakspart
                    {
                        Sak = sak,
                        UnntattOffentlighet = true,
                        Navn = "Ola Normann",
                        Organisasjonsnummer = "01019999999",
                        IdentifikasjonstypeId = "FNR",
                        Postadresse = "Fjellveien 1",
                        Postnummer = "1000",
                        Poststed = "Andeby",
                    };
                    context.Add(sakspart);
                    var sakspartRolleMedlem = new SakspartRolleMedlem
                    {
                        Sakspart = sakspart,
                        RolleId = "KK", //KK=Klient kontakt
                    };
                    context.Add(sakspartRolleMedlem);

                    var merknad = new Merknad
                    {
                        Sak = sak,
                        Tekst = $"Sak opprettet av integrasjonsbruker {nCoreFactory.NCoreSettings.Username}"
                    };
                    context.Add(merknad);

                    //Lagre objekter til server:
                    context.SaveChanges();

                    logger.LogDebug($"Opprettet ny sak: {NCoreHelper.FormatSaksRef(sak)}");
                }
                #endregion

                #region Klassere saker
                var klientsaker = context.Query<Sak>().Include(x => x.Primaerklassering).Where(x => x.Tilleggsattributt1.StartsWith("Klientsak-")).ToList();
                foreach(var sak in klientsaker)
                {
                    if (sak.Primaerklassering != null)
                        continue;

                    //Ordningsprinsipp FNS: Felles arkivnøkkel for statsforvaltingen
                    //Ordningsverdi ADM: Intern administrasjon
                    var ordningsverdi = context.Query<Ordningsverdi>().Where(x => x.Tittel == "ADM" && x.OrdningsprinsippId == "FNS").FirstOrDefault();
                    var primaerKlassering = new Klassering
                    {
                        OrdningsprinsippId = ordningsverdi.OrdningsprinsippId, //"FNS"
                        OrdningsverdiId = ordningsverdi.Tittel, //"ADM"
                        Beskrivelse = ordningsverdi.Beskrivelse, //"Intern administrasjon"
                        SakId = sak.Id,
                        UntattOffentlighet = false,
                        Sortering = "1", //1=Primaerklassering
                        Opprettet = DateTime.Now,
                    };
                    context.Add(primaerKlassering);

                    var sekundaerKlassering = new Klassering
                    {
                        OrdningsprinsippId = "FNKL", //fødselsnr
                        OrdningsverdiId = "01019999999", 
                        Beskrivelse = "Ola Normann", 
                        SakId = sak.Id,
                        UntattOffentlighet = true,
                        Sortering = "2", //2=Sekundaerklassering
                        Opprettet = DateTime.Now,
                    };
                    context.Add(sekundaerKlassering);

                    //Lagre objekter til server:
                    context.SaveChanges();
                    
                    logger.LogInformation($"PrimaerKlassering {primaerKlassering.OrdningsprinsippId}#{primaerKlassering.OrdningsverdiId} opprettet på sak {NCoreHelper.FormatSaksRef(sak)}");
                    logger.LogInformation($"SekundaerKlassering {sekundaerKlassering.OrdningsprinsippId}#{sekundaerKlassering.OrdningsverdiId} opprettet på sak {NCoreHelper.FormatSaksRef(sak)}");
                }
                #endregion

                #region Avskriv og avslutt saker
                var aktiveKlientsaker = context.Query<Sak>().Where(x => x.Tilleggsattributt1.StartsWith("Klientsak-") && x.SaksstatusId != "A" && x.SaksstatusId != "U").ToList();
                foreach (var sak in aktiveKlientsaker)
                {
                    //NCoreHelper.AvskrivOgAvsluttSak(context, sak);
                    //logger.LogDebug($"Avsluttet sak: {NCoreHelper.FormatSaksRef(sak)}");
                }
                #endregion


                #region Opprett utgående journalpost og ekspeder denne
                var eksisterendeSak = context.Query<Sak>().Where(x => x.Tilleggsattributt1 == "Klientsak-000").First();
                var journalpost = new Journalpost
                {
                    SakId = eksisterendeSak.Id,
                    TilgangskodeId = eksisterendeSak.TilgangskodeId,
                    Hjemmel = eksisterendeSak.Hjemmel,
                    Dokumentdato = DateTime.Now,
                    DokumenttypeId = "U",
                    DokumentkategoriId = "VT", //Vedtaksbrev
                    JournalstatusId = "R" //vi starter med R/M og venter med F til alle dokumenter er på plass 
                };
                NCoreHelper.SkjermingOgMarkeringAvTekst($"Vedtak om transport - @Ola Normann@", journalpost);
                context.Add(journalpost);

                #region Legg til ekstern mottaker og angi ekspedering
                var avsMot = new AvsenderMottaker 
                {
                    Innholdstype = true, //true=mottaker,
                    Journalpost = journalpost,
                    Organisasjonsnummer = "01019999999",
                    EPostAdresse = "ola.normann@norge.no",
                    IdentifikasjonstypeId = "FNR",
                    Navn = "Ola Normann",
                    Postadresse = "Fjellveien 1",
                    Postnummer = "1000",
                    Poststed = "Andeby",
                };
                avsMot.ForsendelsesmaateId = "GENERELL";
                avsMot.ForsendelsesstatusId = "K"; //K = klar for sending
                context.Add(avsMot);
                #endregion

                #region Legg til dokumenter
                var dokumenter = new List<Tuple<string, string>>(); //tittel og base64 innhold for TXT-fil
                dokumenter.Add(Tuple.Create("Vedtak hoveddokument", "VGVzdCBIb3ZlZGRva3VtZW50"));
                dokumenter.Add(Tuple.Create("Vedtak vedlegg", "VGVzdCBWZWRsZWdn"));

                foreach (var dok in dokumenter.OrderBy(x => x.Item1)) //hoveddok først
                {
                    var erHoveddokument = dok.Item1.Contains("hoveddokument");

                    var dokumentbeskrivelse = new Dokumentbeskrivelse
                    {
                        Dokumenttittel = dok.Item1,
                        DokumentstatusId = "F"
                    };
                    dokumentbeskrivelse.DokumentkategoriId = journalpost.DokumentkategoriId;
                    dokumentbeskrivelse.Publisert = false;

                    if (erHoveddokument)
                    {
                        dokumentbeskrivelse.TilgangskodeId = journalpost.TilgangskodeId;
                        dokumentbeskrivelse.Hjemmel = journalpost.Hjemmel;
                    }
                    else //vedlegg med en annen tilgangskode
                    {
                        dokumentbeskrivelse.TilgangskodeId = "U";
                        dokumentbeskrivelse.Hjemmel = context.Query<TilgangskodeHjemmel>().Where(x => x.TilgangskodeId == "U" && x.Standard == true).FirstOrDefault()?.Hjemmel;
                    }
                    
                    context.Add(dokumentbeskrivelse);

                    var format = context.Query<Lagringsformat>().Where(x => x.Filtype == "TXT").First();
                    var dokumentversjon = new Dokumentversjon
                    {
                        Dokumentbeskrivelse = dokumentbeskrivelse,
                        LagringsformatId = format.Id,
                        VariantId = "P", 
                    };
                    context.Add(dokumentversjon);

                    using (var contentStream = new MemoryStream(Convert.FromBase64String(dok.Item2)))
                    {
                        context.Documents.Upload(dokumentversjon, contentStream, "dummy.TXT");
                    }

                    var dokumentreferanse = new Dokumentreferanse
                    {
                        Dokumentbeskrivelse = dokumentbeskrivelse,
                        Journalpost = journalpost,
                        TilknytningskodeId = (erHoveddokument ? "H" : "V") //H=Hoveddokument, V=Vedlegg
                    };
                    context.Add(dokumentreferanse);

                    //sende objekter til server for lagring:
                    context.SaveChanges();


                    if (dokumentbeskrivelse.Dokumenttittel != dok.Item1)
                    {
                        //resetter dokumenttittel siden denne automatisk settes lik journalpostittel ved lagring av hoveddokument
                        dokumentbeskrivelse.Dokumenttittel = dok.Item1; 
                        context.SaveChanges();
                    }
                }
                #endregion

                #region Legg til merknad
                var jpMerknad = new Merknad
                {
                    Sak = eksisterendeSak,
                    Journalpost = journalpost,
                    Tekst = $"Journalpost opprettet av integrasjonsbruker {nCoreFactory.NCoreSettings.Username}"
                };
                context.Add(jpMerknad);
                context.SaveChanges();
                #endregion

                if (journalpost.DokumenttypeId == "U")
                {
                    bool sendPaaGodkjenningFoerEkspedering = true;

                    if (sendPaaGodkjenningFoerEkspedering)
                    {
                        //Opprett Godkjenningsflyt på journalpost
                        var godkjennereEpostListe = "demobruker@sikri.no"; // "demobruker@sikri.no,demo_int@sikri.no";
                        NCoreHelper.SendJournalpostPaaGodkjenningsflyt(context, logger, journalpost, godkjennereEpostListe);
                        logger.LogInformation($"Journalpost {NCoreHelper.FormatJournalpostDokRef(eksisterendeSak, journalpost)} sent til godkjenning");
                    }
                    else
                    {
                        //trigger ekspedering med stattus F. Etter ekspedring vil den få status E (ekspedert)
                        journalpost.JournalstatusId = "F"; 
                        context.SaveChanges(); 
                    }



                }

                if (journalpost.DokumenttypeId == "I")
                {
                    NCoreHelper.AvskrivJournalpostDirekte(context, "TE", journalpost.Id); //om man ikke ønsker restanse
                }




                #endregion

                #region Eksempler på søk
                var sakerHvorOlaErSakspart = context.Query<Sakspart>().Include(x => x.Sak).Include(x => x.Sak.AnsvarligEnhet)
                                       .Where(x => x.Organisasjonsnummer == "01019999999" 
                                                   && x.Sak.ArkivdelId == "SAK"
                                                   && x.Sak.MappetypeId == "KSA").ToList();
                
                foreach(var sakspart in sakerHvorOlaErSakspart)
                    logger.LogDebug($"Ola er sakspart i sak {NCoreHelper.FormatSaksRef(sakspart.Sak)}");


                var sakerHvorOlaHarSakspartRolle = context.Query<SakspartRolleMedlem>().Include(x => x.Sakspart).Include(x => x.Sakspart.Sak)
                                       .Where(x => x.Sakspart.Organisasjonsnummer == "01019999999" 
                                                   && x.Sakspart.Sak.ArkivdelId == "SAK"
                                                   && x.Sakspart.Sak.MappetypeId == "KSA"
                                                   && (x.RolleId == "KK")).ToList();

                foreach (var sakspartRolleMedlem in sakerHvorOlaHarSakspartRolle)
                    logger.LogDebug($"Ola har sakspartrolle KK i sak {NCoreHelper.FormatSaksRef(sakspartRolleMedlem.Sakspart.Sak)}");

                var klasserteSaker = context.Query<Klassering>().Include(x => x.Sak)
                                    .Where(x => x.OrdningsverdiId == "ADM" && x.Sortering == "1" && x.Sak.MappetypeId == "KSA").ToList();

                foreach (var klassering in klasserteSaker)
                    logger.LogDebug($"Sak {NCoreHelper.FormatSaksRef(klassering.Sak)} har primærklassering med verdien ADM");

                var sakerMedTilleggsattributt = context.Query<Sak>()
                                    .Where(x => x.Tilleggsattributt1.StartsWith("Klientsak-") && x.ArkivdelId == "SAK" && x.MappetypeId == "KSA").ToList();

                foreach (var sak in sakerMedTilleggsattributt)
                    logger.LogDebug($"Sak {NCoreHelper.FormatSaksRef(sak)} har verdi '{sak.Tilleggsattributt1}' i Tilleggsattributt1");

                #endregion



                #region Last ned dokumentmal 
                var mal = NCoreHelper.HentDokumentmal(context, "Vedtak sensurklage - ingen endring");
                var bytes = Convert.FromBase64String(mal.TemplateFileContentBase64);
                var templateFile = "c:\\temp\\" + mal.TemplateFileName;
                File.WriteAllBytes(templateFile, bytes); //ikke ferdig flettet med tilleggsmal og autotekst
                logger.LogDebug($"Mal lastet ned: {templateFile}");
                #endregion
            }




        }
    }


}
