using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Newtonsoft.Json;
using PokecordCatcherBot.Attributes;
using PokecordCatcherBot.Models;

namespace PokecordCatcherBot.Services
{
    public class CommandService : Service
    {
        private readonly List<MethodInfo> commandMethods;

        public CommandService(PokecordCatcher bot) : base(bot)
        {
            commandMethods = FindCommandMethods();

            Client.MessageReceived += OnMessage;
        }

        private async Task OnMessage(SocketMessage msg)
        {
            if (!msg.Content.StartsWith(Configuration.UserbotPrefix) || msg.Author.Id != Configuration.OwnerID)
                return;

            var args = msg.Content.Split(' ').ToList();
            var commandName = args[0].Substring(Configuration.UserbotPrefix.Length);
            args.RemoveAt(0);

            var command = commandMethods.FirstOrDefault(x => String.Equals(x.GetCustomAttribute<CommandAttribute>().Name, commandName, StringComparison.OrdinalIgnoreCase));

            if (command != null)
            {
                try
                {
                    var cmdTask = (Task)command.Invoke(this, new object[] { msg, args.ToArray() });

                    var task = Task.Run(async () => await cmdTask)
                        .ContinueWith(t => Console.WriteLine(t.Exception.Flatten().InnerException), TaskContinuationOptions.OnlyOnFaulted);
                }
                catch (Exception e) { Console.WriteLine(e); }
            }
        }

        private List<MethodInfo> FindCommandMethods() =>
            typeof(PokecordCatcher).Assembly.GetTypes()
                .SelectMany(x => x.GetMethods())
                .Where(x => x.GetCustomAttribute(typeof(CommandAttribute)) != null).ToList();

        [Command(nameof(Status), "Displays information about the bot's state.")]
        public async Task Status(SocketMessage msg, string[] args)
        {
            var props = typeof(State).GetProperties();
            await msg.Channel.SendMessageAsync($"```{String.Join('\n', props.Select(x => $"{x.Name}: {x.GetValue(State)}"))}```");
        }

        [Command(nameof(Reload), "Reload the bot's configuration.")]
        public async Task Reload(SocketMessage msg, string[] args)
        {
            bot.UpdateConfiguration("config.json");
            await msg.Channel.SendMessageAsync("Configuration reloaded.");
        }

        [Command(nameof(ToggleGuilds), "Toggle guild whitelisting.")]
        public async Task ToggleGuilds(SocketMessage msg, string[] args)
        {
            State.WhitelistGuilds = !State.WhitelistGuilds;
            File.WriteAllText("state.data", JsonConvert.SerializeObject(State));
            await msg.Channel.SendMessageAsync("Whitelisting of guilds has been toggled to " + State.WhitelistGuilds);
        }

        [Command(nameof(TogglePokemon), "Toggle pokemon whitelisting.")]
        public async Task TogglePokemon(SocketMessage msg, string[] args)
        {
            State.WhitelistPokemon = !State.WhitelistPokemon;
            File.WriteAllText("state.data", JsonConvert.SerializeObject(State));
            await msg.Channel.SendMessageAsync("Whitelisting of pokemon has been toggled to " + State.WhitelistPokemon);
        }

        [Command(nameof(ToggleSpam), "Toggle spamming.")]
        public async Task ToggleSpam(SocketMessage msg, string[] args)
        {
            State.SpammerEnabled = !State.SpammerEnabled;
            File.WriteAllText("state.data", JsonConvert.SerializeObject(State));
            await msg.Channel.SendMessageAsync("Spam has been toggled to " + State.SpammerEnabled);
        }

        [Command(nameof(Echo), "Has the bot say something.")]
        public async Task Echo(SocketMessage msg, string[] args) => 
            await msg.Channel.SendMessageAsync(String.Join(' ', args));
			
        [Command(nameof(Say), "Has the bot say something.")]
        public async Task Say(SocketMessage msg, string[] args) => 
            await msg.Channel.SendMessageAsync(String.Join(' ', args));

        [Command(nameof(Display), "Displays all pokemon of a certain name.")]
        public async Task Display(SocketMessage msg, string[] args) => 
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}pokemon --name {String.Join(' ', args)}");

        [Command(nameof(AddId), "Adds a list of specified IDs")]
        public async Task AddId(SocketMessage msg, string[] args) => 
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add {String.Join(' ', args)}");
			
        [Command(nameof(DisplayAll), "Displays all pokemon.")]
        public async Task DisplayAll(SocketMessage msg, string[] args) => 
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}pokemon");
			
        [Command(nameof(Accept), "Runs accept command in pokecord")]
        public async Task Accept(SocketMessage msg, string[] args) => 
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}accept");
			
        [Command(nameof(Confirm), "Confirms the current trade.")]
        public async Task Confirm(SocketMessage msg, string[] args) => 
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}confirm");
			
        [Command(nameof(Shiny), "Checks for shinies.")]
        public async Task Shiny(SocketMessage msg, string[] args) => 
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}pokemon --shiny");
			
        [Command(nameof(StartTrade), "Starts a trade with the bot owner.")]
        public async Task StartTrade(SocketMessage msg, string[] args) => 
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}trade <@{Configuration.OwnerID}>");

        [Command(nameof(Details), "Toggles showing of detailed pokemon stats.")]
        public async Task Details(SocketMessage msg, string[] args) => 
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}detailed");

        [Command(nameof(Exit), "Exits the userbot program.")]
        public async Task Exit(SocketMessage msg, string[] args)
        {
            await Client.LogoutAsync();
            Environment.Exit(0);
        }

        [Command(nameof(Trade), "Trades all pokemon a certain name.")]
        public async Task Trade(SocketMessage msg, string[] args)
        {
            var list = await ResponseGrabber.SendMessageAndGrabResponse(
                (ITextChannel)msg.Channel,
                $"{Configuration.PokecordPrefix}pokemon --name {String.Join(' ', args)}",
                x => MessagePredicates.PokemonListingMessage(x, msg),
                5
            );
			
			await Task.Delay(3000);
			
            if (list == null)
            {
                await msg.Channel.SendMessageAsync("Pokecord didn't display pokemon, aborting.");
                return;
            }

            var pokemans = Util.ParsePokemonListing(list.Embeds.First().Description);

            await Task.Delay(3000);

            var trade = await ResponseGrabber.SendMessageAndGrabResponse(
                (ITextChannel)msg.Channel,
                $"{Configuration.PokecordPrefix}trade <@{Configuration.OwnerID}>",
                x => MessagePredicates.TradeMessage(x, msg),
                5
            );

            await Task.Delay(3000);

            if (trade == null)
            {
                await msg.Channel.SendMessageAsync("p!cancel");
                return;
            }

            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add {String.Join(' ', pokemans.Select(x => x.Id))}");
            await Task.Delay(1000);
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}confirm");
            await Task.Delay(1500);
        }

        [Command(nameof(TradeById), "Trades pokemon with specified ids.")]
        public async Task TradeById(SocketMessage msg, string[] args)
        {
            
            var trade = await ResponseGrabber.SendMessageAndGrabResponse(
                (ITextChannel)msg.Channel,
                $"{Configuration.PokecordPrefix}trade <@{Configuration.OwnerID}>",
                x => MessagePredicates.TradeMessage(x, msg),
                5
            );

            await Task.Delay(3000);

            if (trade == null)
            {
                await msg.Channel.SendMessageAsync("p!cancel");
                return;
            }

            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add {String.Join(' ', args)}");
            await Task.Delay(1000);
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}confirm");
            await Task.Delay(1500);
        }
		
		[Command(nameof(Trade100), "Trades pokemon with specified ids.")]
        public async Task Trade100(SocketMessage msg, string[] args)
        {
            
            var trade = await ResponseGrabber.SendMessageAndGrabResponse(
                (ITextChannel)msg.Channel,
                $"{Configuration.PokecordPrefix}trade <@{Configuration.OwnerID}>",
                x => MessagePredicates.TradeMessage(x, msg),
                5
            );

            await Task.Delay(3000);

            if (trade == null)
            {
                await msg.Channel.SendMessageAsync("p!cancel");
                return;
            }

            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20 21 22 23 24 25");
            await Task.Delay(1523);
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add 26 27 28 29 30 31 32 33 34 35 36 37 38 39 40 41 42 43 44 45 46 47 48 49 50");
            await Task.Delay(1927);
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add 51 52 53 54 55 56 57 58 59 60 61 62 63 64 65 66 67 68 69 70 71 72 73 74 75");
            await Task.Delay(1750);
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add 76 77 78 79 80 81 82 83 84 85 86 87 88 89 90 91 92 93 94 95 96 97 98 99 100");
            await Task.Delay(1853);
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}confirm");
            await Task.Delay(1582);
        }
		
		[Command(nameof(Trade200), "Trades pokemon with specified ids.")]
        public async Task Trade200(SocketMessage msg, string[] args)
        {
            
            var trade = await ResponseGrabber.SendMessageAndGrabResponse(
                (ITextChannel)msg.Channel,
                $"{Configuration.PokecordPrefix}trade <@{Configuration.OwnerID}>",
                x => MessagePredicates.TradeMessage(x, msg),
                5
            );

            await Task.Delay(3000);

            if (trade == null)
            {
                await msg.Channel.SendMessageAsync("p!cancel");
                return;
            }

            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20 21 22 23 24 25");
            await Task.Delay(1523);
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add 26 27 28 29 30 31 32 33 34 35 36 37 38 39 40 41 42 43 44 45 46 47 48 49 50");
            await Task.Delay(1927);
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add 51 52 53 54 55 56 57 58 59 60 61 62 63 64 65 66 67 68 69 70 71 72 73 74 75");
            await Task.Delay(1750);
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add 76 77 78 79 80 81 82 83 84 85 86 87 88 89 90 91 92 93 94 95 96 97 98 99 100");
            await Task.Delay(1853);
			await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add 101 102 103 104 105 106 107 108 109 110 111 112 113 114 115 116 117 118 119 120 121 122 123 124 125");
            await Task.Delay(2836);
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add 126 127 128 129 130 131 132 133 134 135 136 137 138 139 140 141 142 143 144 145 146 147 148 149 150");
            await Task.Delay(1457);
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add 151 152 153 154 155 156 157 158 159 160 161 162 163 164 165 166 167 168 169 170 171 172 173 174 175");
            await Task.Delay(1953);
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add 176 177 178 179 180 181 182 183 184 185 186 187 188 189 190 191 192 193 194 195 196 197 198 199 200");
            await Task.Delay(2563);
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}confirm");
            await Task.Delay(1636);
        }
		
		[Command(nameof(Trade325), "Trades pokemon with specified ids.")]
        public async Task Trade325(SocketMessage msg, string[] args)
        {
            
            var trade = await ResponseGrabber.SendMessageAndGrabResponse(
                (ITextChannel)msg.Channel,
                $"{Configuration.PokecordPrefix}trade <@{Configuration.OwnerID}>",
                x => MessagePredicates.TradeMessage(x, msg),
                5
            );

            await Task.Delay(3000);

            if (trade == null)
            {
                await msg.Channel.SendMessageAsync("p!cancel");
                return;
            }

            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15 16 17 18 19 20 21 22 23 24 25");
            await Task.Delay(1523);
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add 26 27 28 29 30 31 32 33 34 35 36 37 38 39 40 41 42 43 44 45 46 47 48 49 50");
            await Task.Delay(1927);
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add 51 52 53 54 55 56 57 58 59 60 61 62 63 64 65 66 67 68 69 70 71 72 73 74 75");
            await Task.Delay(1750);
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add 76 77 78 79 80 81 82 83 84 85 86 87 88 89 90 91 92 93 94 95 96 97 98 99 100");
            await Task.Delay(1853);
			await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add 101 102 103 104 105 106 107 108 109 110 111 112 113 114 115 116 117 118 119 120 121 122 123 124 125");
            await Task.Delay(2534);
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add 126 127 128 129 130 131 132 133 134 135 136 137 138 139 140 141 142 143 144 145 146 147 148 149 150");
            await Task.Delay(1463);
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add 151 152 153 154 155 156 157 158 159 160 161 162 163 164 165 166 167 168 169 170 171 172 173 174 175");
            await Task.Delay(2684);
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add 176 177 178 179 180 181 182 183 184 185 186 187 188 189 190 191 192 193 194 195 196 197 198 199 200");
            await Task.Delay(1636);
			await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add 201 202 203 204 205 206 207 208 209 210 211 212 213 214 215 216 217 218 219 220 221 222 223 224 225");
            await Task.Delay(2337);
			await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add 226 227 228 229 230 231 232 233 234 235 236 237 238 239 240 241 242 243 244 245 246 247 248 249 250");
            await Task.Delay(1478);
			await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add 251 252 253 254 255 256 257 258 259 260 261 262 263 264 265 266 267 268 269 270 271 272 273 274 275");
            await Task.Delay(1735);
			await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add 276 277 278 279 280 281 282 283 284 285 286 287 288 289 290 291 292 293 294 295 296 297 298 299 300");
            await Task.Delay(1468);
			await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}p add 301 302 303 304 305 306 307 308 309 310 311 312 313 314 315 316 317 318 319 320 321 322 323 324 325");
            await Task.Delay(1735);
            await msg.Channel.SendMessageAsync($"{Configuration.PokecordPrefix}confirm");
            await Task.Delay(1736);
        }

    }
}
