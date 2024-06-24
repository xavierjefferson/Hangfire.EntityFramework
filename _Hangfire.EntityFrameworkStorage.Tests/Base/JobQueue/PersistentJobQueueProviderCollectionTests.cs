﻿using System;
using System.Linq;
using Hangfire.EntityFrameworkStorage.JobQueue;
using Moq;
using Xunit;

namespace Hangfire.EntityFrameworkStorage.Tests.Base.JobQueue
{
    public class PersistentJobQueueProviderCollectionTests
    {
        public PersistentJobQueueProviderCollectionTests()
        {
            _defaultProvider = new Mock<IPersistentJobQueueProvider>();
            _provider = new Mock<IPersistentJobQueueProvider>();
        }

        private static readonly string[] _queues = {"default", "critical"};
        private readonly Mock<IPersistentJobQueueProvider> _defaultProvider;
        private readonly Mock<IPersistentJobQueueProvider> _provider;

        private PersistentJobQueueProviderCollection CreateCollection()
        {
            return new PersistentJobQueueProviderCollection(_defaultProvider.Object);
        }

        [Fact]
        public void Add_ThrowsAnException_WhenProviderIsNull()
        {
            var collection = CreateCollection();

            var exception = Assert.Throws<ArgumentNullException>(
                () => collection.Add(null, _queues));

            Assert.Equal("provider", exception.ParamName);
        }

        [Fact]
        public void Add_ThrowsAnException_WhenQueuesCollectionIsNull()
        {
            var collection = CreateCollection();

            var exception = Assert.Throws<ArgumentNullException>(
                () => collection.Add(_provider.Object, null));

            Assert.Equal("queues", exception.ParamName);
        }

        [Fact]
        public void Ctor_ThrowsAnException_WhenDefaultProviderIsNull()
        {
            Assert.Throws<ArgumentNullException>(
                () => new PersistentJobQueueProviderCollection(null));
        }

        [Fact]
        public void Enumeration_ContainsAddedProvider()
        {
            var collection = CreateCollection();

            collection.Add(_provider.Object, _queues);

            Assert.Contains(_provider.Object, collection);
        }

        [Fact]
        public void Enumeration_IncludesTheDefaultProvider()
        {
            var collection = CreateCollection();

            var result = collection.ToArray();

            Assert.Single(result);
            Assert.Same(_defaultProvider.Object, result[0]);
        }

        [Fact]
        public void GetProvider_CanBeResolved_ByAnyQueue()
        {
            var collection = CreateCollection();
            collection.Add(_provider.Object, _queues);

            var provider1 = collection.GetProvider("default");
            var provider2 = collection.GetProvider("critical");

            Assert.NotSame(_defaultProvider.Object, provider1);
            Assert.Same(provider1, provider2);
        }

        [Fact]
        public void GetProvider_ReturnsTheDefaultProvider_WhenProviderCanNotBeResolvedByQueue()
        {
            var collection = CreateCollection();

            var provider = collection.GetProvider("queue");

            Assert.Same(_defaultProvider.Object, provider);
        }
    }
}