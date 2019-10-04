using System;
using Akka.Actor;
using Lib.AspNetCore.ServerSentEvents;
using AkkaFractal.Core;
using static AkkaFractal.Core.ColorConsole;


namespace AkkaFractal.Web.Akka
{
    public delegate IActorRef SseTileActorProvider();

    public class SseTileActor : ReceiveActor
    {
        private IActorRef renderActor;
        public SseTileActor(IServerSentEventsService serverSentEventsService, IActorRef tileRenderActor)
        {
            Receive<RenderImage>(request =>
            {
                var split = 20;
                var ys = request.Height / split;
                var xs = request.Width / split;

                WriteLineYellow($"Starting image processing size Width {request.Width} - Height {request.Height}");
                

                if (Context.Child("RenderActor").Equals(ActorRefs.Nobody))
                {
                    WriteLineYellow($"Creating child actor RenderActor");

                    renderActor =
                        Context.ActorOf(
                            Props.Create(() =>
                                new RenderActor(serverSentEventsService, request.Width, request.Height, split)),
                            "RenderActor");
                }

                for (var y = 0; y < split; y++)
                {
                    var yy = ys * y;
                    for (var x = 0; x < split; x++)
                    {
                        var xx = xs * x;
                       
                        tileRenderActor.Tell(new RenderTile(yy, xx, xs, ys, request.Height, request.Width)
                            , renderActor);
                    }
                    
                    // if (y / split == 0)
                    // {
                    //      WriteLineCyan($"simulating Actor crash");
                    //      tileRenderActor.Tell(new SimulateError());
                    // }
                }

                
                tileRenderActor.Tell(new Completed(), renderActor);
                WriteLineYellow($"Image processing completed");
            });
        }
    }
}