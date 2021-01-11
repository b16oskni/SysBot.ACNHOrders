﻿using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.ACNHOrders
{
    /// <summary>
    /// Red's dodo retrieval and movement code
    /// </summary>
    public class LoopHelpers
    {
        private readonly SwitchConnectionAsync Connection;
        private readonly CrossBot BotRunner;
        private readonly CrossBotConfig Config;

        public string DodoCode { get; set; } = "No code set yet."; 
        public ulong CoordinateAddress { get; set; } = 0x0;
        public byte[] InitialPlayerX { get; set; } = new byte[2];
        public byte[] InitialPlayerY { get; set; } = new byte[2];

        public LoopHelpers(CrossBot bot)
        {
            BotRunner = bot;
            Connection = BotRunner.Connection;
            Config = BotRunner.Config;
        }

        public async Task<ulong> GetCoordinateAddress(string pointer, CancellationToken token)
        {
            // Regex pattern to get operators and offsets from pointer expression.	
            string pattern = @"(\+|\-)([A-Fa-f0-9]+)";
            Regex regex = new Regex(pattern);
            Match match = regex.Match(pointer);

            // Get first offset from pointer expression and read address at that offset from main start.	
            var ofs = Convert.ToUInt64(match.Groups[2].Value, 16);
            var address = BitConverter.ToUInt64(await Connection.ReadBytesMainAsync(ofs, 0x8, token).ConfigureAwait(false), 0);
            match = match.NextMatch();

            // Matches the rest of the operators and offsets in the pointer expression.	
            while (match.Success)
            {
                // Get operator and offset from match.	
                string opp = match.Groups[1].Value;
                ofs = Convert.ToUInt64(match.Groups[2].Value, 16);

                // Add or subtract the offset from the current stored address based on operator in front of offset.	
                switch (opp)
                {
                    case "+":
                        address += ofs;
                        break;
                    case "-":
                        address -= ofs;
                        break;
                }

                // Attempt another match and if successful read bytes at address and store the new address.	
                match = match.NextMatch();
                if (match.Success)
                {
                    byte[] bytes = await Connection.ReadBytesAbsoluteAsync(address, 0x8, token).ConfigureAwait(false);
                    address = BitConverter.ToUInt64(bytes, 0);
                }
            }

            return address;
        }

        private async Task GetDodoCode(uint Offset, CancellationToken token)
        {
            // Teleport player to airport entrance and set rotation to face doorway.	
            await Connection.WriteBytesAbsoluteAsync(new byte[] { 64, 68, 0, 0, 0, 0, 0, 0, 132, 68 }, CoordinateAddress, token).ConfigureAwait(false);
            await Connection.WriteBytesAbsoluteAsync(new byte[] { 0, 0, 0, 112 }, CoordinateAddress + 0x3A, token).ConfigureAwait(false);

            // Walk through airport entrance.	
            await BotRunner.SetStick(SwitchStick.LEFT, 20_000, 20_000, 1_000, token).ConfigureAwait(false);
            await BotRunner.SetStick(SwitchStick.LEFT, 0, 0, 9_000, token).ConfigureAwait(false);

            // Get player's coordinate address when inside airport and teleport player to Dodo.	
            var AirportCoordinateAddress = await GetCoordinateAddress(Config.CoordinatePointer, token).ConfigureAwait(false);
            await Connection.WriteBytesAbsoluteAsync(new byte[] { 58, 67, 0, 0, 0, 0, 0, 0, 38, 67 }, AirportCoordinateAddress, token).ConfigureAwait(false);

            // Navigate through dialog with Dodo to open gates and to get Dodo code.	
            var Hold = SwitchCommand.Hold(SwitchButton.L);
            await Connection.SendAsync(Hold, token).ConfigureAwait(false);
            await Task.Delay(0_500).ConfigureAwait(false);
            await BotRunner.Click(SwitchButton.A, 3_500, token).ConfigureAwait(false);
            await BotRunner.Click(SwitchButton.A, 1_500, token).ConfigureAwait(false);
            await BotRunner.Click(SwitchButton.A, 1_500, token).ConfigureAwait(false);
            await BotRunner.Click(SwitchButton.DDOWN, 0_300, token).ConfigureAwait(false);
            await BotRunner.Click(SwitchButton.A, 2_500, token).ConfigureAwait(false);
            await BotRunner.Click(SwitchButton.A, 1_000, token).ConfigureAwait(false);
            await BotRunner.Click(SwitchButton.DDOWN, 0_300, token).ConfigureAwait(false);
            await BotRunner.Click(SwitchButton.A, 2_000, token).ConfigureAwait(false);
            await BotRunner.Click(SwitchButton.A, 1_000, token).ConfigureAwait(false);
            await BotRunner.Click(SwitchButton.A, 12_000, token).ConfigureAwait(false);
            await BotRunner.Click(SwitchButton.A, 1_500, token).ConfigureAwait(false);
            await BotRunner.Click(SwitchButton.DDOWN, 0_300, token).ConfigureAwait(false);
            await BotRunner.Click(SwitchButton.DDOWN, 0_300, token).ConfigureAwait(false);
            await BotRunner.Click(SwitchButton.A, 1_500, token).ConfigureAwait(false);
            await BotRunner.Click(SwitchButton.A, 1_000, token).ConfigureAwait(false);
            await BotRunner.Click(SwitchButton.DUP, 0_300, token).ConfigureAwait(false);
            await BotRunner.Click(SwitchButton.A, 2_500, token).ConfigureAwait(false);
            await BotRunner.Click(SwitchButton.A, 1_000, token).ConfigureAwait(false);
            await BotRunner.Click(SwitchButton.A, 1_500, token).ConfigureAwait(false);
            await BotRunner.Click(SwitchButton.A, 2_500, token).ConfigureAwait(false);
            await BotRunner.Click(SwitchButton.A, 3_000, token).ConfigureAwait(false);
            await BotRunner.Click(SwitchButton.A, 2_000, token).ConfigureAwait(false);
            await BotRunner.Click(SwitchButton.A, 2_000, token).ConfigureAwait(false);
            var Release = SwitchCommand.Release(SwitchButton.L);
            await Connection.SendAsync(Release, token).ConfigureAwait(false);

            // Obtain Dodo code from offset and store it.	
            byte[] bytes = await Connection.ReadBytesAsync(Offset, 0x5, token).ConfigureAwait(false);
            DodoCode = System.Text.Encoding.UTF8.GetString(bytes, 0, 5);
            LogUtil.LogInfo($"Retrieved Dodo code: {DodoCode}.", Config.IP);

            // Teleport into warp zone to leave airport	
            await Connection.WriteBytesAbsoluteAsync(new byte[] { 32, 67, 0, 0, 0, 0, 0, 0, 120, 67 }, AirportCoordinateAddress, token).ConfigureAwait(false);

            // Wait for loading screen.	
            while (!await IsOverworld(token))
                await Task.Delay(0_500).ConfigureAwait(false);
        }

        private async Task ResetPosition(CancellationToken token)
        {
            // Sets player xy coordinates to their initial values when bot was started and set player rotation to 0.	
            await Connection.WriteBytesAbsoluteAsync(new byte[] { InitialPlayerX[0], InitialPlayerX[1], 0, 0, 0, 0, 0, 0, InitialPlayerY[0], InitialPlayerY[1] }, CoordinateAddress, token).ConfigureAwait(false);
            await Connection.WriteBytesAbsoluteAsync(new byte[] { 0, 0, 0, 0 }, CoordinateAddress + 0x3A, token).ConfigureAwait(false);
        }

        private async Task<bool> IsOverworld(CancellationToken token)
        {
            var x = BitConverter.ToUInt32(await Connection.ReadBytesAbsoluteAsync(CoordinateAddress + 0x1E, 0x4, token).ConfigureAwait(false), 0);
            return x == 0xC0066666;
        }
    }
}
