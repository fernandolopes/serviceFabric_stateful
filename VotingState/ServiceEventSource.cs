using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Fabric;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Runtime;

namespace VotingState
{
    [EventSource(Name = "MyCompany-Voting-VotingState")]
    internal sealed class ServiceEventSource : EventSource
    {
        public static readonly ServiceEventSource Current = new ServiceEventSource();

        static ServiceEventSource()
        {
            // Uma alternativa para o problema no qual atividades ETW não são acompanhadas até que a infraestrutura de Tarefas seja inicializada.
            // Este problema será corrigido no .NET Framework 4.6.2.
            Task.Run(() => { });
        }

        // O construtor de instância é particular para impor semânticas de singleton
        private ServiceEventSource() : base() { }

        #region Palavras-chave
        // Palavras-chave de evento podem ser usadas para categorizar eventos. 
        // Cada palavra-chave é um sinalizador de bit. Um único evento pode ser associado a várias palavras-chave (por meio da propriedade EventAttribute.Keywords).
        // As palavras-chave devem ser definidas como uma classe pública chamada "Keywords" dentro da EventSource que as utiliza.
        public static class Keywords
        {
            public const EventKeywords Requests = (EventKeywords)0x1L;
            public const EventKeywords ServiceInitialization = (EventKeywords)0x2L;
        }
        #endregion

        #region Eventos
        // Defina um método de instância para cada evento que você deseja registrar e aplique um atributo [Event] a ele.
        // O nome do método é o nome do evento.
        // Transmita os parâmetros que você deseja registrar com o evento (apenas tipos inteiros primitivos, DateTime, Guid e cadeias de caracteres são permitidos).
        // Cada implementação do método de evento deve verificar se a origem do evento está habilitada e, se estiver, deve chamar o método WriteEvent() para acionar o evento.
        // O número e os tipos de argumentos transmitidos para cada método de evento devem corresponder exatamente ao que é transmitido para WriteEvent().
        // Coloque o atributo [NonEvent] em todos os métodos que não definem um evento.
        // Para obter mais informações, consulte https://msdn.microsoft.com/pt-br/library/system.diagnostics.tracing.eventsource.aspx

        [NonEvent]
        public void Message(string message, params object[] args)
        {
            if (this.IsEnabled())
            {
                string finalMessage = string.Format(message, args);
                Message(finalMessage);
            }
        }

        private const int MessageEventId = 1;
        [Event(MessageEventId, Level = EventLevel.Informational, Message = "{0}")]
        public void Message(string message)
        {
            if (this.IsEnabled())
            {
                WriteEvent(MessageEventId, message);
            }
        }

        [NonEvent]
        public void ServiceMessage(StatefulServiceContext serviceContext, string message, params object[] args)
        {
            if (this.IsEnabled())
            {
                string finalMessage = string.Format(message, args);
                ServiceMessage(
                    serviceContext.ServiceName.ToString(),
                    serviceContext.ServiceTypeName,
                    serviceContext.ReplicaId,
                    serviceContext.PartitionId,
                    serviceContext.CodePackageActivationContext.ApplicationName,
                    serviceContext.CodePackageActivationContext.ApplicationTypeName,
                    serviceContext.NodeContext.NodeName,
                    finalMessage);
            }
        }

        // Para eventos de altíssima frequência, pode ser útil acionar eventos usando a API WriteEventCore.
        // Isso leva a um manuseio de parâmetros mais eficiente, porém requer alocação explícita da estrutura de EventData e código não seguro.
        // Para habilitar esse caminho de código, defina o símbolo de compilação condicional como NÃO SEGURO e ative o suporte a código não seguro nas propriedades do projeto.
        private const int ServiceMessageEventId = 2;
        [Event(ServiceMessageEventId, Level = EventLevel.Informational, Message = "{7}")]
        private
#if UNSAFE
        unsafe
#endif
        void ServiceMessage(
            string serviceName,
            string serviceTypeName,
            long replicaOrInstanceId,
            Guid partitionId,
            string applicationName,
            string applicationTypeName,
            string nodeName,
            string message)
        {
#if !UNSAFE
            WriteEvent(ServiceMessageEventId, serviceName, serviceTypeName, replicaOrInstanceId, partitionId, applicationName, applicationTypeName, nodeName, message);
#else
            const int numArgs = 8;
            fixed (char* pServiceName = serviceName, pServiceTypeName = serviceTypeName, pApplicationName = applicationName, pApplicationTypeName = applicationTypeName, pNodeName = nodeName, pMessage = message)
            {
                EventData* eventData = stackalloc EventData[numArgs];
                eventData[0] = new EventData { DataPointer = (IntPtr) pServiceName, Size = SizeInBytes(serviceName) };
                eventData[1] = new EventData { DataPointer = (IntPtr) pServiceTypeName, Size = SizeInBytes(serviceTypeName) };
                eventData[2] = new EventData { DataPointer = (IntPtr) (&replicaOrInstanceId), Size = sizeof(long) };
                eventData[3] = new EventData { DataPointer = (IntPtr) (&partitionId), Size = sizeof(Guid) };
                eventData[4] = new EventData { DataPointer = (IntPtr) pApplicationName, Size = SizeInBytes(applicationName) };
                eventData[5] = new EventData { DataPointer = (IntPtr) pApplicationTypeName, Size = SizeInBytes(applicationTypeName) };
                eventData[6] = new EventData { DataPointer = (IntPtr) pNodeName, Size = SizeInBytes(nodeName) };
                eventData[7] = new EventData { DataPointer = (IntPtr) pMessage, Size = SizeInBytes(message) };

                WriteEventCore(ServiceMessageEventId, numArgs, eventData);
            }
#endif
        }

        private const int ServiceTypeRegisteredEventId = 3;
        [Event(ServiceTypeRegisteredEventId, Level = EventLevel.Informational, Message = "Service host process {0} registered service type {1}", Keywords = Keywords.ServiceInitialization)]
        public void ServiceTypeRegistered(int hostProcessId, string serviceType)
        {
            WriteEvent(ServiceTypeRegisteredEventId, hostProcessId, serviceType);
        }

        private const int ServiceHostInitializationFailedEventId = 4;
        [Event(ServiceHostInitializationFailedEventId, Level = EventLevel.Error, Message = "Service host initialization failed", Keywords = Keywords.ServiceInitialization)]
        public void ServiceHostInitializationFailed(string exception)
        {
            WriteEvent(ServiceHostInitializationFailedEventId, exception);
        }

        private const int ServiceRequestStartEventId = 5;
        [Event(ServiceRequestStartEventId, Level = EventLevel.Informational, Message = "Service request '{0}' started. {1}", Keywords = Keywords.Requests)]
        public void ServiceRequestStart(string requestTypeName, string activity)
        {
            WriteEvent(ServiceRequestStartEventId, requestTypeName, activity);
        }

        private const int ServiceRequestStopEventId = 6;
        [Event(ServiceRequestStopEventId, Level = EventLevel.Informational, Message = "Service request '{0}' finished. {1}", Keywords = Keywords.Requests)]
        public void ServiceRequestStop(string requestTypeName, string activity, string exception = "")
        {
            WriteEvent(ServiceRequestStopEventId, requestTypeName, activity, exception);
        }


        // Um par de eventos que compartilha o mesmo prefixo de nome com um sufixo "Start"/"Stop" marca implicitamente os limites de uma atividade de rastreamento de evento.
        // Essas atividades podem ser coletadas automaticamente pelas ferramentas de depuração e criação de perfil, as quais podem computar a hora de execução, atividades filho,
        // e outras estatísticas.
        //private const int ServiceRequestStartEventId = 5;
        //[Event(ServiceRequestStartEventId, Level = EventLevel.Informational, Message = "Service request '{0}' started", Keywords = Keywords.Requests)]
        //public void ServiceRequestStart(string requestTypeName)
        //{
        //    WriteEvent(ServiceRequestStartEventId, requestTypeName);
        //}

        //private const int ServiceRequestStopEventId = 6;
        //[Event(ServiceRequestStopEventId, Level = EventLevel.Informational, Message = "Service request '{0}' finished", Keywords = Keywords.Requests)]
        //public void ServiceRequestStop(string requestTypeName, string exception = "")
        //{
        //    WriteEvent(ServiceRequestStopEventId, requestTypeName, exception);
        //}
        #endregion

        #region Métodos particulares
#if UNSAFE
        private int SizeInBytes(string s)
        {
            if (s == null)
            {
                return 0;
            }
            else
            {
                return (s.Length + 1) * sizeof(char);
            }
        }
#endif
        #endregion
    }
}
