﻿using System;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Transactions;
using WebBackgrounder.EntityFramework.Entities;

namespace WebBackgrounder.EntityFramework
{
    public class EntityWorkItemRepository : IWorkItemRepository
    {
        Func<IWorkItemsContext> _contextThunk;
        IWorkItemsContext _context;

        public EntityWorkItemRepository(Func<IWorkItemsContext> contextThunk)
        {
            _contextThunk = contextThunk;
            _context = _contextThunk();
        }

        public void RunInTransaction(Action query)
        {
            using (var transaction = new TransactionScope())
            {
                // For some reason, I get different behavior when I use this
                // instead of _context.Database.Connection. This works, that doesn't. :(
                ((IObjectContextAdapter)_context).ObjectContext.Connection.Open();
                query();
                transaction.Complete();
            }
            // REVIEW: Make sure this is really needed. I kept running into 
            // exceptions when I didn't do this, but I may be doing it wrong. -Phil 10/17/2011
            _context.Dispose();
            _context = _contextThunk();
        }

        public bool AnyActiveWorker(string jobName)
        {
            var activeWorker = GetActiveWorkItem(jobName);
            if (activeWorker != null)
            {
                // TODO: Handle work item expiration.
                return true;
            }
            return false;
        }

        private WorkItem GetActiveWorkItem(string jobName)
        {
            return (from w in _context.WorkItems
                    where w.JobName == jobName
                          && w.Completed == null
                    select w).FirstOrDefault();
        }

        public long CreateWorkItem(string workerId, string jobName)
        {
            var workItem = new WorkItem
            {
                JobName = jobName,
                WorkerId = workerId,
                Started = DateTime.UtcNow,
                Completed = null
            };
            _context.WorkItems.Add(workItem);
            _context.SaveChanges();
            return workItem.Id;
        }

        public void SetWorkItemCompleted(long workItemId)
        {
            var workItem = GetWorkItem(workItemId);
            workItem.Completed = DateTime.UtcNow;
            _context.SaveChanges();
        }

        public void SetWorkItemFailed(long workItemId, Exception exception)
        {
            var workItem = GetWorkItem(workItemId);
            workItem.Completed = DateTime.UtcNow;
            workItem.ExceptionInfo = exception.Message + Environment.NewLine + exception.StackTrace;
            _context.SaveChanges();
        }

        private WorkItem GetWorkItem(long workerId)
        {
            return _context.WorkItems.Find(workerId);
        }

        public void Dispose()
        {
            var context = _context;
            if (context != null)
            {
                context.Dispose();
            }
        }
    }
}