using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Akka.Actor;
using Akka.Event;
using Akka.Logger.Serilog;
using Akka.Routing;
using Serilog;

namespace AkkaActorSystem
{
    class Program
    {

        static void Main(string[] args)
        {
            var logger = new LoggerConfiguration()
                .ReadFrom.AppSettings()
                .Enrich.WithProperty("Version", Assembly.GetEntryAssembly().GetName().Version)
                .CreateLogger();

            Serilog.Log.Logger = logger;

            var config = new StreamReader("akkaconfig.json").ReadToEnd();
            var system = ActorSystem.Create("ActorSystem", config);
            var coordinator = system.ActorOf<CoordinatorActor>("Coordinator");

            coordinator.Tell(1);

            system.AwaitTermination();
        }
    }

    public class CoordinatorActor : ReceiveActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger(new SerilogLogMessageFormatter());
        private readonly IActorRef _workerActor;
        public CoordinatorActor()
        {
            var props =
                Props.Create<WorkerActor>()
                    .WithRouter(FromConfig.Instance)
                    .WithSupervisorStrategy(new OneForOneStrategy(ex => Directive.Restart));
            _workerActor = Context.ActorOf(props, "Worker");
            Receive<int>(info => HandleRequest(info));
        }

        protected override void PreStart()
        {
            _log.Debug("Prestart");
        }

        private void HandleRequest(int info)
        {
            Thread.Sleep(TimeSpan.FromSeconds(10));
            _log.Info("*************************************");
            _log.Info($"Coordinator Recieved {info}");
            _workerActor.Tell(info);
        }

    }

    public class WorkerActor : ReceiveActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger(new SerilogLogMessageFormatter());
        public WorkerActor()
        {
            Receive<int>(info => Handle(info));
        }

        private void Handle(int info)
        {
            try
            {
                _log.Info($"{Context.Self.Path} Recieved {info}");
                if (info % 2 == 0)
                {
                    _log.Info("even");
                    Thread.Sleep(TimeSpan.FromSeconds(3));
                    _log.Info($"{Context.Self.Path} done");
                }
                else
                {
                    _log.Info("odd");
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                    throw new Exception("Invalid data");
                }

                _log.Info("*************************************");
            }
            catch (Exception ex)
            {
                _log.Error(ex.Message);
                throw;
            }
        }
    }
}
