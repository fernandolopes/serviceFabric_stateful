using Microsoft.ServiceFabric.Services.Runtime;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace VotingService
{
    internal static class Program
    {
        /// <summary>
        /// Este é o ponto de entrada do processo de host de serviço.
        /// </summary>
        private static void Main()
        {
            try
            {
                // O arquivo ServiceManifest.XML define um ou mais nomes de tipo de serviço.
                // Registrar um serviço mapeia um nome de tipo de serviço para um tipo de .NET.
                // Quando o Service Fabric cria uma instância deste tipo de serviço,
                // uma instância da classe é criada neste processo de host.

                ServiceRuntime.RegisterServiceAsync("VotingServiceType",
                    context => new VotingService(context)).GetAwaiter().GetResult();

                ServiceEventSource.Current.ServiceTypeRegistered(Process.GetCurrentProcess().Id, typeof(VotingService).Name);

                // Impede que este processo de host seja encerrado para que os serviços continuem em execução. 
                Thread.Sleep(Timeout.Infinite);
            }
            catch (Exception e)
            {
                ServiceEventSource.Current.ServiceHostInitializationFailed(e.ToString());
                throw;
            }
        }
    }
}
