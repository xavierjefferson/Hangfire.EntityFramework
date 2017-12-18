﻿using Hangfire.FluentNHibernateStorage.Entities;

namespace Hangfire.FluentNHibernateStorage.Maps
{
    internal class _JobStateMap : IntIdMap<_JobState>
    {
        public _JobStateMap()
        {
            Table("Hangfire_JobState".WrapObjectName());

            Map(i => i.Name).Column("Name".WrapObjectName()).Length(20).Not.Nullable();
            Map(i => i.Reason).Column("Reason".WrapObjectName()).Length(100).Nullable();
            Map(i => i.CreatedAt).Column(Constants.CreatedAt).Not.Nullable();
            Map(i => i.Data).Column(Constants.Data).Length(Constants.VarcharMaxLength).Nullable();
            References(i => i.Job).Column(Constants.JobId).Not.Nullable().Cascade.Delete();
        }
    }
}