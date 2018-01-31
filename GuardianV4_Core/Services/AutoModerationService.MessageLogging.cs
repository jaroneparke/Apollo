﻿using Discord;
using Discord.WebSocket;
using GuardianV4_Core.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GuardianV4_Core.Services
{
    public partial class AutoModerationService
    {
        public class MessageObject
        {
            public ulong Id { get; set; }
            public IUser Author { get; set; }
            public string Content { get; set; }
            public ISocketMessageChannel Channel { get; set; }
            public IReadOnlyCollection<Attachment> Attachments { get; set; }
            public DateTimeOffset Timestamp { get; internal set; }
        }

        Dictionary<ulong, List<MessageObject>> _messageLists = new Dictionary<ulong, List<MessageObject>>();

        private async Task CacheMessage(SocketMessage arg)
        {
            if ((arg.Author is SocketGuildUser user))
            {
                var deleteLogChannel = user.Guild.GetDeleteLogChannel();
                if (deleteLogChannel == null)
                {
                    return;
                }

                var msgList = GetOrCreateMessageList(user.Guild.Id);

                if (msgList != null)
                {
                    msgList.Add(new MessageObject
                    {
                        Author = arg.Author,
                        Attachments = arg.Attachments,
                        Channel = arg.Channel,
                        Content = arg.Content,
                        Id = arg.Id,
                        Timestamp = arg.Timestamp
                    });

                    if (msgList.Count > 10000)
                    {
                        msgList.RemoveAt(0);
                    }
                }
            }
        }

        private async Task LogEditedMessage(Cacheable<IMessage, ulong> arg1, SocketMessage arg2, ISocketMessageChannel arg3)
        {
            var channel = arg3 as SocketTextChannel;
            if (channel == null)
            {
                return;
            }
            if (!_messageLists.ContainsKey(channel.Guild.Id))
            {
                return;
            }

            var msg = _messageLists[(channel.Guild.Id)].FirstOrDefault(x => x.Id == arg1.Id);


            var deleteLogChannel = (arg2?.Author as SocketGuildUser)?.Guild.GetDeleteLogChannel();
            if (deleteLogChannel == null)
            {
                return;
            }

            if (msg == null || msg.Content == arg2.Content)
            {
                return;
            }

            string beforeAttachments = "\n";
            string afterAttachments = "\n";


            foreach (var attachment in msg.Attachments)
            {
                beforeAttachments += $"{attachment.Url}\n";
            }
            foreach (var attachment in arg2.Attachments)
            {
                afterAttachments += $"{attachment.Url}\n";
            }

            var embed = new EmbedBuilder()
                .WithEmbedType(EmbedType.MessageEdited, arg2.Author)
                .WithDescription($"**Message edited in {(arg2.Channel as SocketTextChannel).Mention} (sent at {msg.Timestamp:dd-MMM-yyyy HH:mm:ss})**")
                .AddField("Old content", msg.Content + beforeAttachments)
                .AddField("New content", arg2.Content + afterAttachments)
                .WithMessageLogFooter(arg2.Author, msg)
                .Build();

            await deleteLogChannel.SendMessageAsync("", embed: embed);
            msg.Content = arg2.Content;

        }

        private async Task LogDeletedMessage(Cacheable<IMessage, ulong> arg1, ISocketMessageChannel arg2)
        {
            var channel = arg2 as SocketTextChannel;
            if (channel == null)
            {
                return;
            }
            if (!_messageLists.ContainsKey(channel.Guild.Id))
            {
                return;
            }

            var msg = _messageLists[(channel.Guild.Id)].FirstOrDefault(x => x.Id == arg1.Id);

            if (msg == null)
            {
                return;
            }

            var deleteLogChannel = (msg.Author as SocketGuildUser)?.Guild.GetDeleteLogChannel();
            if (deleteLogChannel == null)
            {
                return;
            }

            string beforeAttachments = "\n";
            string afterAttachments = "\n";


            foreach (var attachment in msg.Attachments)
            {
                beforeAttachments += $"{attachment.Url}\n";
            }

            var embed = new EmbedBuilder()
                .WithEmbedType(EmbedType.MessageDeleted, msg.Author)
                .WithDescription($"**Message deleted in {(msg.Channel as SocketTextChannel).Mention} (sent at {msg.Timestamp:dd-MMM-yyyy HH:mm:ss})**")
                .AddField("Content", msg.Content + beforeAttachments)
                .WithMessageLogFooter(msg.Author, msg)
                .Build();

            await deleteLogChannel.SendMessageAsync("", embed: embed);

            
        }



        private List<MessageObject> GetOrCreateMessageList(ulong guildId)
        {
            if (!_messageLists.ContainsKey(guildId))
            {
                _messageLists.Add(guildId, new List<MessageObject>());
            }
            return _messageLists[guildId];
        }
    }
}
