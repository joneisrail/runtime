// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
#if USE_ETW // TODO: Enable when TraceEvent is available on CoreCLR. GitHub issue https://github.com/dotnet/corefx/issues/4864 
using Microsoft.Diagnostics.Tracing.Session;
#endif
using Xunit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BasicEventSourceTests
{
    public class TestEventCounter
    {
        private sealed class MyEventSource : EventSource
        {
            private EventCounter _requestCounter;
            private EventCounter _errorCounter;

            public MyEventSource()
            {
                _requestCounter = new EventCounter("Request", this);
                _errorCounter = new EventCounter("Error", this);
            }

            public void Request(float elapsed)
            {
                _requestCounter.WriteMetric(elapsed);
            }

            public void Error()
            {
                _errorCounter.WriteMetric(1);
            }
        }

        [Fact]
        public void Test_Write_Metric_EventListener()
        {
            using (var listener = new EventListenerListener())
            {
                Test_Write_Metric(listener);
            }
        }

#if USE_ETW
        [Fact]
        public void Test_Write_Metric_ETW()
        {

            using (var listener = new EtwListener())
            {
                Test_Write_Metric(listener);
            }
        }
#endif

        private void Test_Write_Metric(Listener listener)
        {

            Console.WriteLine("Version of Runtime {0}", Environment.Version);
            Console.WriteLine("Version of OS {0}", Environment.OSVersion);
            TestUtilities.CheckNoEventSourcesRunning("Start");

            using (var logger = new MyEventSource())
            {
                var tests = new List<SubTest>();
                /*************************************************************************/
                tests.Add(new SubTest("Log 1 event",
                    delegate ()
                    {
                        listener.EnableTimer(logger, 0.2); /* Poll every 200 msec */
                        logger.Request(5);
                        Sleep(280); // Sleep for 280 msec
                        listener.EnableTimer(logger, 0);
                    },
                    delegate (List<Event> evts)
                    {
                        // There will be two events (request and error) for time 0 and 2 more at 1 second and 2 more when we shut it off.  
                        Assert.Equal(6, evts.Count);
                        ValidateSingleEventCounter(evts[0], "Request", 0, 0, 0, 0, 0);
                        ValidateSingleEventCounter(evts[1], "Error", 0, 0, 0, 0, 0);
                        ValidateSingleEventCounter(evts[2], "Request", 1, 5, 0, 5, 5);
                        ValidateSingleEventCounter(evts[3], "Error", 0, 0, 0, 0, 0);
                        ValidateSingleEventCounter(evts[4], "Request", 0, 0, 0, 0, 0);
                        ValidateSingleEventCounter(evts[5], "Error", 0, 0, 0, 0, 0);
                    }));
                /*************************************************************************/
                tests.Add(new SubTest("Log 2 event in single period",
                    delegate ()
                    {
                        listener.EnableTimer(logger, 0.2); /* Poll every .2 s */
                        logger.Request(5);
                        logger.Request(10);
                        Sleep(280); // Sleep for .28 seconds
                        listener.EnableTimer(logger, 0);
                    },
                    delegate (List<Event> evts)
                    {
                        Assert.Equal(6, evts.Count);
                        ValidateSingleEventCounter(evts[0], "Request", 0, 0, 0, 0, 0);
                        ValidateSingleEventCounter(evts[1], "Error", 0, 0, 0, 0, 0);
                        ValidateSingleEventCounter(evts[2], "Request", 2, 7.5f, 2.5f, 5, 10);
                        ValidateSingleEventCounter(evts[3], "Error", 0, 0, 0, 0, 0);
                        ValidateSingleEventCounter(evts[4], "Request", 0, 0, 0, 0, 0);
                        ValidateSingleEventCounter(evts[5], "Error", 0, 0, 0, 0, 0);
                    }));
                /*************************************************************************/
                tests.Add(new SubTest("Log 2 event in two periods",
                    delegate ()
                    {
                        listener.EnableTimer(logger, .4); /* Poll every .4 s */
                                                          // logs at 0 seconds because of EnableTimer command
                        logger.Request(5);
                        // At .4 sec we log by timer 
                        Sleep(600); // Sleep for .6 seconds 
                        logger.Request(10);
                        // at .8 sec we log by timer
                        Sleep(400); // Sleep for .4 seconds (at time = 1.0 second exactly 6 messages should be received)
                        // logs at 1.0 seconds because of EnableTimer command
                        listener.EnableTimer(logger, 0);
                    },
                    delegate (List<Event> evts)
                    {
                        Assert.Equal(8, evts.Count);
                        ValidateSingleEventCounter(evts[0], "Request", 0, 0, 0, 0, 0);
                        ValidateSingleEventCounter(evts[1], "Error", 0, 0, 0, 0, 0);
                        ValidateSingleEventCounter(evts[2], "Request", 1, 5, 0, 5, 5);
                        ValidateSingleEventCounter(evts[3], "Error", 0, 0, 0, 0, 0);
                        ValidateSingleEventCounter(evts[4], "Request", 1, 10, 0, 10, 10);
                        ValidateSingleEventCounter(evts[5], "Error", 0, 0, 0, 0, 0);
                        ValidateSingleEventCounter(evts[6], "Request", 0, 0, 0, 0, 0);
                        ValidateSingleEventCounter(evts[7], "Error", 0, 0, 0, 0, 0);
                    }));
                /*************************************************************************/
                tests.Add(new SubTest("Log 2 different events in a period",
                    delegate ()
                    {
                        listener.EnableTimer(logger, .2); /* Poll every .2 s */
                        logger.Request(25);
                        logger.Error();
                        Sleep(280); // Sleep for .28 seconds
                        listener.EnableTimer(logger, 0);
                    },
                    delegate (List<Event> evts)
                    {
                        Assert.Equal(6, evts.Count);
                        ValidateSingleEventCounter(evts[0], "Request", 0, 0, 0, 0, 0);
                        ValidateSingleEventCounter(evts[1], "Error", 0, 0, 0, 0, 0);
                        ValidateSingleEventCounter(evts[2], "Request", 1, 25, 0, 25, 25);
                        ValidateSingleEventCounter(evts[3], "Error", 1, 1, 0, 1, 1);
                        ValidateSingleEventCounter(evts[4], "Request", 0, 0, 0, 0, 0);
                        ValidateSingleEventCounter(evts[5], "Error", 0, 0, 0, 0, 0);
                    }));

                /*************************************************************************/
                tests.Add(new SubTest("Explicit polling ",
                    delegate ()
                    {
                        listener.EnableTimer(logger, 0);  /* Turn off (but also poll once) */
                        logger.Request(5);
                        logger.Request(10);
                        logger.Error();
                        listener.EnableTimer(logger, 0);  /* Turn off (but also poll once) */
                        logger.Request(8);
                        logger.Error();
                        logger.Error();
                        listener.EnableTimer(logger, 0);  /* Turn off (but also poll once) */
                    },
                    delegate (List<Event> evts)
                    {
                        Assert.Equal(6, evts.Count);
                        ValidateSingleEventCounter(evts[0], "Request", 0, 0, 0, 0, 0);
                        ValidateSingleEventCounter(evts[1], "Error", 0, 0, 0, 0, 0);
                        ValidateSingleEventCounter(evts[2], "Request", 2, 7.5f, 2.5f, 5, 10);
                        ValidateSingleEventCounter(evts[3], "Error", 1, 1, 0, 1, 1);
                        ValidateSingleEventCounter(evts[4], "Request", 1, 8, 0, 8, 8);
                        ValidateSingleEventCounter(evts[5], "Error", 2, 1, 0, 1, 1);
                    }));

                /*************************************************************************/
                // TODO expose Dispose() method and activate this test.  
#if EventCounterDispose
                tests.Add(new SubTest("EventCounter.Dispose()",
                    delegate ()
                    {
                        // Creating and destroying 
                        var myCounter = new EventCounter("counter for a transient object", logger);
                        myCounter.WriteMetric(10);
                        listener.EnableTimer(logger, 0);  /* Turn off (but also poll once) */
                        myCounter.Dispose();
                        listener.EnableTimer(logger, 0);  /* Turn off (but also poll once) */
                    },
                    delegate (List<Event> evts)
                    {
                        Assert.Equal(5, evts.Count);
                        ValidateSingleEventCounter(evts[0], "Request", 0, 0, 0, 0, 0);
                        ValidateSingleEventCounter(evts[1], "Error", 0, 0, 0, 0, 0);
                        ValidateSingleEventCounter(evts[2], "counter for a transient object", 1, 10, 0, 10, 10);
                        ValidateSingleEventCounter(evts[3], "Request", 0, 0, 0, 0, 0);
                        ValidateSingleEventCounter(evts[4], "Error", 0, 0, 0, 0, 0);
                    }));
#endif 
                /*************************************************************************/
                EventTestHarness.RunTests(tests, listener, logger);
            }
            TestUtilities.CheckNoEventSourcesRunning("Stop");
        }

        // Thread.Sleep has proven unreliable, sometime sleeping much shorter than it should. 
        // This makes sure it at least sleeps 'msec' at a miniumum.  
        private static void Sleep(int minMSec)
        {
            var startTime = DateTime.UtcNow;
            for(;;)
            {
                DateTime endTime = DateTime.UtcNow;
                double delta = (endTime - startTime).TotalMilliseconds;
                if (delta >= minMSec)
                {
                    Console.WriteLine("Sleep asked to wait {0} msec, actually waited {1:n2} msec Start: {2:mm:ss.fff} End: {3:mm:ss.fff} ", minMSec, delta, startTime, endTime);
                    break;
                }
                Thread.Sleep(1);
            }
        }

        private static void ValidateSingleEventCounter(Event evt, string counterName, int count, float mean, float standardDeviation, float min, float max)
        {
            object payload = ValidateEventHeaderAndGetPayload(evt);
            var payloadContent = payload as IDictionary<string, object>;
            Assert.NotNull(payloadContent);
            ValidateEventCounter(counterName, count, mean, standardDeviation, min, max, payloadContent);
        }

        private static object ValidateEventHeaderAndGetPayload(Event evt)
        {
            Assert.Equal("EventCounters", evt.EventName);
            Assert.Equal(1, evt.PayloadCount);
            Assert.NotNull(evt.PayloadNames);
            Assert.Equal(1, evt.PayloadNames.Count);
            Assert.Equal("Payload", evt.PayloadNames[0]);
            object rawPayload = evt.PayloadValue(0, "Payload");
            return rawPayload;
        }

        private static void ValidateEventCounter(string counterName, int count, float mean, float standardDeviation, float min, float max, IDictionary<string, object> payloadContent)
        {
            Assert.Equal(counterName, (string)payloadContent["Name"]);
            Assert.Equal(count, (int)payloadContent["Count"]);
            if (count != 0)
            {
                Assert.Equal(mean, (float)payloadContent["Mean"]);
                Assert.Equal(standardDeviation, (float)payloadContent["StandardDeviation"]);
            }
            Assert.Equal(min, (float)payloadContent["Min"]);
            Assert.Equal(max, (float)payloadContent["Max"]);
        }
    }
}
