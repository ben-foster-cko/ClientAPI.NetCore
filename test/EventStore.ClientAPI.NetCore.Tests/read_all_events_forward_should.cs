using System;
using System.Collections.Generic;
using System.Linq;
using Eventstore.ClientAPI.Tests.Helpers;
using EventStore.ClientAPI;
using NUnit.Framework;

namespace Eventstore.ClientAPI.Tests
{
    [TestFixture, Category("LongRunning")]
    public class read_all_events_forward_should : SpecificationWithConnection
    {
        private EventData[] _testEvents;
        private Position _from;

        protected override void When()
        {
            _conn.SetStreamMetadataAsync("$all", ExpectedVersion.Any,
                                    StreamMetadata.Build().SetReadRole(SystemRoles.All),
                                    DefaultData.AdminCredentials)
            .Wait();
            var result = _conn.ReadAllEventsBackwardAsync(Position.End, 1, false).Result;
            _from = result.NextPosition;
            _testEvents = Enumerable.Range(0, 20).Select(x => TestEvent.NewTestEvent(x.ToString())).ToArray();
            _conn.AppendToStreamAsync("read_all_events_forward_should", ExpectedVersion.EmptyStream, _testEvents).Wait();
        }

        [OneTimeTearDown]
        public override void OneTimeTearDown()
        {
            _conn.SetStreamMetadataAsync("$all", ExpectedVersion.Any,
                StreamMetadata.Build(),
                DefaultData.AdminCredentials);
        }

        [Test, Category("LongRunning")]
        public void return_empty_slice_if_asked_to_read_from_end()
        {
            var read = _conn.ReadAllEventsForwardAsync(Position.End, 1, false).Result;
            Assert.That(read.IsEndOfStream, Is.True);
            Assert.That(read.Events.Length, Is.EqualTo(0));
        }

        [Test, Category("LongRunning")]
        public void return_events_in_same_order_as_written()
        {
            var read = _conn.ReadAllEventsForwardAsync(_from, _testEvents.Length + 10, false).Result;
            Assert.That(EventDataComparer.Equal(
                _testEvents.ToArray(),
                read.Events.Skip(read.Events.Length - _testEvents.Length).Select(x => x.Event).ToArray()));
        }

        [Test, Category("LongRunning")]
        public void be_able_to_read_all_one_by_one_until_end_of_stream()
        {
            var all = new List<RecordedEvent>();
            var position = _from;
            AllEventsSlice slice;

            while (!(slice = _conn.ReadAllEventsForwardAsync(position, 1, false).Result).IsEndOfStream)
            {
                all.Add(slice.Events.Single().Event);
                position = slice.NextPosition;
            }

            Assert.That(EventDataComparer.Equal(_testEvents, all.Skip(all.Count - _testEvents.Length).ToArray()));
        }

        [Test, Category("LongRunning")]
        public void be_able_to_read_events_slice_at_time()
        {
            var all = new List<RecordedEvent>();
            var position = _from;
            AllEventsSlice slice;

            while (!(slice = _conn.ReadAllEventsForwardAsync(position, 5, false).Result).IsEndOfStream)
            {
                all.AddRange(slice.Events.Select(x => x.Event));
                position = slice.NextPosition;
            }

            Assert.That(EventDataComparer.Equal(_testEvents, all.Skip(all.Count - _testEvents.Length).ToArray()));
        }

        [Test, Category("LongRunning")]
        public void return_partial_slice_if_not_enough_events()
        {
            var read = _conn.ReadAllEventsForwardAsync(_from, 30, false).Result;
            Assert.That(read.Events.Length, Is.LessThan(30));
            Assert.That(EventDataComparer.Equal(
                _testEvents,
                read.Events.Skip(read.Events.Length - _testEvents.Length).Select(x => x.Event).ToArray()));
        }

        [Test]
        [Category("Network")]
        public void throw_when_got_int_max_value_as_maxcount()
        {
            Assert.Throws<ArgumentException>(
                () => _conn.ReadAllEventsForwardAsync(Position.Start, int.MaxValue, resolveLinkTos: false));

        }
    }
}
