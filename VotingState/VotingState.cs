using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace VotingState
{
    /// <summary>
    /// Uma instância desta classe é criada para cada réplica de serviço pelo tempo de execução do Service Fabric.
    /// </summary>
    internal sealed class VotingState : StatefulService, IVotingService
    {
        //private DateTime data = DateTime.Now;

        public VotingState(StatefulServiceContext context, InitializationCallbackAdapter adapter)
            : base(context, new ReliableStateManager(context, 
                new ReliableStateManagerConfiguration(onInitializeStateSerializersEvent: adapter.OnInitialize)))
        {
            adapter.StateManager = this.StateManager;
        }

        /// <summary>
        /// Substituição opcional para criar ouvintes (por exemplo, HTTP, Serviço de Comunicação Remota, WCF, etc.) para esta réplica de serviço para manipular as solicitações do cliente ou do usuário.
        /// </summary>
        /// <remarks>
        /// Para obter mais informações sobre comunicação de serviços, consulte https://aka.ms/servicefabricservicecommunication
        /// </remarks>
        /// <returns>Uma coleção de ouvintes.</returns>
        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return new ServiceReplicaListener[]
            {
                new ServiceReplicaListener(serviceContext => new OwinCommunicationListener(
                    serviceContext, this, ServiceEventSource.Current, "ServiceEndpoint"))
            };
        }

        /// <summary>
        /// Este é o principal ponto de entrada da sua réplica de serviço.
        /// Esse método é executado quando esta réplica do seu serviço torna-se a primária e tem status de gravação.
        /// </summary>
        /// <param name="cancellationToken">Cancelado quando o Service Fabric precisa desligar esta réplica de serviço.</param>
        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            // Server=localhost\SQLEXPRESS;Database=master;Trusted_Connection=True;
            // TODO: substitua o código de exemplo a seguir pela sua própria lógica 
            //       ou remova esta substituição RunAsync se ela não for necessária no seu serviço.
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                //if(DateTime.Now >= data)
                //{
                //    data = DateTime.Now.AddMinutes(10);
                //    Console.WriteLine($"vai rodar: {data} entrou");
                //}
            }

        }

        /// <summary>
        /// Gets the count dictionary. If it doesn't already exist it is created.
        /// </summary>
        /// <remarks>This is a good example of how to initialize reliable collections. 
        /// Rather than initializing and caching a value, this approach will work on 
        /// both primary and secondary replicas (if secondary reads are enabled.</remarks>
        private async Task<IReliableDictionary<string, VotingData>> GetCountDictionaryAsync()
        {
            return await StateManager
                .GetOrAddAsync<IReliableDictionary<string, VotingData>>("votingCountDictionary")
                .ConfigureAwait(false);
        }

        // Track the number of requests to the controller.
        private long _requestCount = 0;

        // Returns or increments the request count.
        public long RequestCount
        {
            get { return Volatile.Read(ref _requestCount); }
            set { Interlocked.Increment(ref _requestCount); }
        }

        /// <summary>
        /// Gets the list of VotingData items.
        /// </summary>
        public async Task<IReadOnlyList<VotingData>> GetVotingDataAsync(string id, CancellationToken token)
        {
            List<VotingData> items = new List<VotingData>();
            ServiceEventSource.Current.ServiceRequestStart("VotingState.GetVotingDataAsync", id);

            // Get the dictionary.
            var dictionary = await GetCountDictionaryAsync();
            using (ITransaction tx = StateManager.CreateTransaction())
            {
                // Create the enumerable and get the enumerator.
                var enumItems = (await dictionary.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                while (await enumItems.MoveNextAsync(token))
                {
                    items.Add(enumItems.Current.Value);
                    if (items.Count > 1000)
                        break;
                }
            }

            return items;
        }

        /// <summary>
        /// Updates the count on a vote or adds a new vote if it doesn't already exist.
        /// </summary>
        public async Task AddVoteAsync(string key, int count, string id, CancellationToken token)
        {
            ServiceEventSource.Current.ServiceRequestStart("VotingState.AddVoteAsync", id);

            // Get the dictionary.
            var dictionary = await GetCountDictionaryAsync();
            using (ITransaction tx = StateManager.CreateTransaction())
            {
                // Try to get the existing value
                ConditionalValue<VotingData> result = await dictionary.TryGetValueAsync(tx, key, LockMode.Update);
                if (result.HasValue)
                {
                    // VotingData is immutable to ensure the reference returned from the dictionary
                    // isn't modified. If it were not immutable, you changed a field’s value, and then the transaction aborts, the value will 
                    // remain modified in memory, corrupting your data.
                    VotingData newData = result.Value.UpdateWith(result.Value.Count + count);
                    await dictionary.TryUpdateAsync(tx, key, newData, result.Value);
                }
                else
                {
                    // Add a new VotingData item to the collection
                    await dictionary.AddAsync(tx, key, new VotingData(key, count, count, DateTimeOffset.Now));
                }

                // Commit the transaction.
                await tx.CommitAsync();
            }
        }

    }
}
