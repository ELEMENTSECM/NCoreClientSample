using Gecko.NCore.Client;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.ServiceModel;
using Gecko.NCore.Client.ObjectModel;

namespace NCoreClientSample
{

    public class NCoreFactory
    {
        private readonly ILogger _logger;
        public readonly NCoreSettings NCoreSettings;

        private const string DocumentServiceAddress = "services/documents/v2/DocumentService.svc";
        private const string FunctionServiceAddressV2 = "Services/Functions/V2/FunctionsService.svc";

        private const string ObjectModelServiceAddress = "services/objectmodel/v3/no/ObjectModelService.svc"; //used with package/namespace Gecko.NCore.Client.ObjectModel.V3.No
        //private const string ObjectModelServiceAddress = "services/objectmodel/v3/en/ObjectModelService.svc"; //used with package/namespace Gecko.NCore.Client.ObjectModel.V3.En

        public NCoreFactory(ILogger<NCoreFactory> logger, IOptions<NCoreSettings> ncoreSettings)
        {
            _logger = logger;
            NCoreSettings = ncoreSettings?.Value;
        }

        public IEphorteContext Create()
        {

            var ephorteContextIdentity = new EphorteContextIdentity
            {
                Username = NCoreSettings.Username,
                Password = NCoreSettings.Password,
                Role = NCoreSettings.Role,
                Database = NCoreSettings.Database,
                ExternalSystemName = NCoreSettings.ExternalSystemName,
            };

            var documentsAdapter = CreateAdapter(DocumentServiceAddress, clientSetting => new Gecko.NCore.Client.Documents.V2.DocumentsAdapter(ephorteContextIdentity, clientSetting));
            var functionsAdapter = CreateAdapter(FunctionServiceAddressV2, clientSetting => new Gecko.NCore.Client.Functions.V2.AsyncFunctionsAdapter(ephorteContextIdentity, clientSetting));
            IObjectModelAdapter objectModelAdapter;
            if (ObjectModelServiceAddress.ToLower().Contains("/no/"))
                objectModelAdapter = CreateAdapter(ObjectModelServiceAddress, clientSetting => new Gecko.NCore.Client.ObjectModel.V3.No.AsyncObjectModelAdapterV3No(ephorteContextIdentity, clientSetting));
            else
                objectModelAdapter = CreateAdapter(ObjectModelServiceAddress, clientSetting => new Gecko.NCore.Client.ObjectModel.V3.En.AsyncObjectModelAdapterV3En(ephorteContextIdentity, clientSetting));
            

            return new EphorteContext(objectModelAdapter, functionsAdapter, documentsAdapter: documentsAdapter, documentsAdapterWithDatabase: null, metadataAdapter: null);
        }

        private T CreateAdapter<T>(string address, Func<ClientSettings, T> createAdapterFunc)
        {
            var clientSettings = new ClientSettings
            {
                Address = new Uri(new Uri(NCoreSettings.BaseAddress), address).ToString(),
            };
            return createAdapterFunc(clientSettings);
        }


       

    }
}
