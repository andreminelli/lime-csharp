﻿using System.Threading.Tasks;

namespace Lime.Protocol.Listeners
{
    /// <summary>
    /// Defines a channel listener service.
    /// </summary>
    /// <seealso cref="IStartable" />
    /// <seealso cref="IStoppable" />
    public interface IChanneListener : IStartable, IStoppable
    {
        /// <summary>
        /// Gets the message listener task. 
        /// When completed, return the last unconsumed <see cref="Message"/>, if there's any.
        /// </summary>
        Task<Message> MessageListenerTask { get; }

        /// <summary>
        /// Gets the notification listener task.
        /// When completed, return the last unconsumed <see cref="Notification"/>, if there's any.
        /// </summary>
        Task<Notification> NotificationListenerTask { get; }

        /// <summary>
        /// Gets the command listener task.
        /// When completed, return the last unconsumed <see cref="Command"/>, if there's any.
        /// </summary>
        Task<Command> CommandListenerTask { get; }
    }
}