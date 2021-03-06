﻿using GalaSoft.MvvmLight;
using Lime.Protocol;
using Lime.Messaging.Contents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lime.Client.Windows.ViewModels
{
    public class MessageViewModel : ViewModelBase
    {
        #region Constructor

        public MessageViewModel(Message message, MessageDirection direction, bool isUnreaded)            
        {
            if (message == null) throw new ArgumentNullException(nameof(message));           
            Id = message.Id;
            Direction = direction;
            IsUnreaded = isUnreaded;

            if (message.Content is PlainText)
            {
                Text = ((PlainText)message.Content).Text;
            }
            else
            {
                Text = "(Not supported content type)";
            }
        }

        public MessageViewModel()
        {
            Timestamp = DateTime.Now;
            Id = EnvelopeId.NewId();
        }

        #endregion


        private string _id;
        public string Id
        {
            get { return _id; }
            set
            {
                _id = value;
                RaisePropertyChanged(() => Id);
            }
        }

        private Event? _lastEvent;
        public Event? LastEvent
        {
            get { return _lastEvent; }
            set
            {
                if (_lastEvent == null || 
                    (_lastEvent != Event.Failed && (value == Event.Failed || value > _lastEvent)))
                {
                    _lastEvent = value;
                    RaisePropertyChanged(() => LastEvent);
                }
            }
        }

        private string _text;
        public string Text
        {
            get { return _text; }
            set
            {
                _text = value;
                RaisePropertyChanged(() => Text);
            }
        }

        private DateTime _timestamp;
        public DateTime Timestamp
        {
            get { return _timestamp; }
            set
            {
                _timestamp = value;
                RaisePropertyChanged(() => Timestamp);
            }
        }

        private MessageDirection _direction;
        public MessageDirection Direction
        {
            get { return _direction; }
            set
            {
                _direction = value;
                if (value == MessageDirection.Output) _isUnreaded = false;
                RaisePropertyChanged(() => Direction);
            }
        }

        private bool _isUnreaded;
        public bool IsUnreaded
        {
            get { return _isUnreaded; }
            set
            {
                _isUnreaded = value;
                RaisePropertyChanged(() => IsUnreaded);
            }
        }

    }


    public enum MessageDirection
    {
        Input,
        Output
    }
}
