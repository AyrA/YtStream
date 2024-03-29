﻿using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using YtStream.Code;
using YtStream.Interfaces;

namespace YtStream.Models
{
    public class StreamOptionsModel : IValidateable
    {
        /// <summary>
        /// Maximum number of seconds the client can buffer ahead
        /// </summary>
        public const int MaxBufferSize = 20;
        /// <summary>
        /// Default buffer size for networked stream
        /// </summary>
        public const int DefaultBufferSize = 5;
        /// <summary>
        /// Maximum number of repetitions
        /// </summary>
        public const int MaxRepetitions = 99;

        public StreamOptionsModel()
        {
            Repeat = 1;
            Buffer = -1;
        }

        public StreamOptionsModel(IQueryCollection query) : this()
        {
            if (query != null)
            {
                var t = GetType();
                var props = t.GetProperties();
                var keys = query.Keys.DistinctBy(m => m.ToLower()).ToList();
                foreach (var key in keys)
                {
                    var value = query[key];
                    var prop = props.FirstOrDefault(m => m.Name.ToLower() == key.ToLower());
                    if (prop == null || value.Count == 0 || string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }
                    if (prop.PropertyType == typeof(string))
                    {
                        prop.SetValue(this, (string?)value);
                    }
                    else if (prop.PropertyType == typeof(int))
                    {
                        if (int.TryParse(value, out int intVal))
                        {
                            prop.SetValue(this, intVal);
                        }
                    }
                    else if (prop.PropertyType == typeof(bool))
                    {
                        if (int.TryParse(value, out int boolVal))
                        {
                            prop.SetValue(this, boolVal != 0);
                        }
                        else
                        {
                            switch (value.ToString().ToLower().Trim())
                            {
                                case "true":
                                case "y":
                                case "yes":
                                    prop.SetValue(this, true);
                                    break;
                            }
                        }
                    }
                }
                Repeat = Math.Min(MaxRepetitions, Math.Max(1, Repeat));
                if (Buffer == -1)
                {
                    Buffer = DefaultBufferSize;
                }
                else
                {
                    Buffer = Math.Min(MaxBufferSize, Math.Max(1, Buffer));
                }
            }
        }

        public StreamOptionsModel(ApiBoolEnum? stream, int? buffer, int? repeat, ApiBoolEnum? random, ApiBoolEnum? raw)
        {
            Repeat = repeat ?? 1;
            Buffer = buffer ?? DefaultBufferSize;
            Random = EnumToBool(random);
            Raw = EnumToBool(raw);
            Stream = EnumToBool(stream);
        }

        private static bool EnumToBool(ApiBoolEnum? value)
        {
            return (value ?? ApiBoolEnum.N) == ApiBoolEnum.Y;
        }

        public bool IsValid()
        {
            return GetValidationMessages().Length == 0;
        }

        public string[] GetValidationMessages()
        {
            var msg = new List<string>();
            if (Repeat < 1 || Repeat > MaxRepetitions)
            {
                msg.Add($"Repetition count must be at least 1 and at most {MaxRepetitions}");
            }
            if (Buffer < 0 || Buffer > MaxBufferSize)
            {
                msg.Add($"Buffer size must be at least 1 and at most {MaxBufferSize}. " +
                    $"The default is {DefaultBufferSize}");
            }
            return msg.ToArray();
        }

        /// <summary>
        /// Gets the number of repetitions in the range of 1 to <see cref="MaxRepetitions"/> inclusive
        /// </summary>
        public int Repeat { get; private set; }
        /// <summary>
        /// Gets the amount of data to buffer for the client in the range of 1 to <see cref="MaxBufferSize"/> inclusive.
        /// The value is in seconds and defaults to <see cref="DefaultBufferSize"/>
        /// </summary>
        /// <remarks>Has no effect if <see cref="Stream"/> is false</remarks>
        public int Buffer { get; private set; }
        /// <summary>
        /// Send data to the client as fast as it's consumed rather than as fast as produced.
        /// Ideal for actual streaming applications
        /// </summary>
        public bool Stream { get; private set; }
        /// <summary>
        /// Send streaming data raw to the client, meaning no cutting will be performed
        /// </summary>
        public bool Raw { get; private set; }
        /// <summary>
        /// Randomize list before each iteration
        /// </summary>
        public bool Random { get; private set; }
    }
}
