using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace AIChatbotHorror
{
    // ===============================================================
    // Global Enums and Data Classes
    // ===============================================================
    
    enum ChatbotTone { Friendly, Ambiguous, Sinister }

    class DialogueNode
    {
        public string Message { get; set; }
        public Dictionary<string, DialogueNode> Responses { get; set; }
        public int MinimumAwareness { get; set; }

        public DialogueNode(string message, int minAwareness = 0)
        {
            Message = message;
            MinimumAwareness = minAwareness;
            Responses = new Dictionary<string, DialogueNode>();
        }
    }

    class Room
    {
        public string Name { get; }
        public string Description { get; }
        public Dictionary<string, string> Exits { get; }
        public bool RequiresKeycard { get; }
        public List<string> Objects { get; }

        public Room(string name, string description, bool requiresKeycard = false, List<string>? objects = null)
        {
            Name = name;
            Description = description;
            Exits = new Dictionary<string, string>();
            RequiresKeycard = requiresKeycard;
            Objects = objects ?? new List<string>();
        }
    }

    // ===============================================================
    // Main Program Class
    // ===============================================================
    class Program
    {
        // ---------------------------------------------------------------
        // Global State Variables
        // ---------------------------------------------------------------
        static bool exitGame = false;
        static bool introductionShown = false;
        static int awarenessLevel = 0;
        static int sanityLevel = 100;
        static ChatbotTone chatbotTone = ChatbotTone.Friendly;
        static List<string> conversationHistory = new List<string>();
        static string playerName = string.Empty;
        static List<string> inventory = new List<string>();
        static List<string> systemLogs = new List<string>();
        static bool diagnosticsRun = false;
        static DialogueNode? currentNode;
        static Stack<DialogueNode> previousNodes = new Stack<DialogueNode>();
        static int turnCount = 0;
        static Dictionary<int, Action> timedEvents = new Dictionary<int, Action>();
        static bool glitchMode = false;
        static Random rnd = new Random();
        const string saveFile = "savegame.txt";

        // Rooms
        static Dictionary<string, Room> rooms = new Dictionary<string, Room>();
        static string currentRoom = "Lobby";

        // ===============================================================
        // Main Entry Point
        // ===============================================================
        static void Main(string[] args)
        {
            RegisterRooms();
            RegisterTimedEvents();
            UI.ClearScreen();
            UI.PrintTitle();
            LoadGameOption();
            GetPlayerName();
            EnterRoom(currentRoom);

            // Run the interactive tutorial unless skipped
            RunInteractiveTutorial();

            while (!exitGame)
            {
                if (!introductionShown)
                {
                    DisplayIntroduction();
                    introductionShown = true;
                }

                turnCount++;
                CheckTimedEvents();

                UI.PrintPrompt();
                string input = UI.GetInput();
                conversationHistory.Add($"Player: {input}");

                ProcessPlayerInput(input);
                ApplyPlayerConsequence(input);
                UpdateChatbotState();
                DisplayChatbotResponse();

                if (currentNode != null && currentNode.Responses.Any())
                {
                    InteractiveDialogue();
                    DisplayChatbotResponse();
                }

                TriggerEndGame();

                if (turnCount % 5 == 0)
                    AutoSaveGame();
            }

            SaveConversationHistory();
            UI.PrintColored($"\nGame Over. Thank you for playing, {playerName}!", ConsoleColor.Magenta);
        }

        // ===============================================================
        // Interactive Tutorial System
        // ===============================================================
        static void RunInteractiveTutorial()
        {
            UI.PrintColored("\nWould you like to play through an interactive tutorial to learn the game mechanics? (y/n): ", ConsoleColor.Cyan);
            string choice = UI.GetInput().ToLower();
            if (choice.StartsWith("n"))
            {
                UI.PrintColored("Tutorial skipped. Type 'tutorial' at any time for a reference guide.", ConsoleColor.Cyan);
                return;
            }

            UI.PrintColored("\n=== Interactive Tutorial ===", ConsoleColor.Cyan);
            UI.PrintResponse("Welcome to SYNAPSE! This tutorial will guide you through the core mechanics. Follow the instructions and type the commands exactly as shown.");

            // Reset state for tutorial
            inventory.Clear();
            currentRoom = "Lobby";
            awarenessLevel = 0;
            sanityLevel = 100;
            chatbotTone = ChatbotTone.Friendly;
            currentNode = null; // Reset dialogue state
            previousNodes.Clear(); // Clear previous dialogue nodes
            BuildDialogueTree(); // Initialize dialogue tree to prevent null currentNode
            EnterRoom(currentRoom);

            // Step 1: Look around
            UI.PrintColored("\nStep 1: Look around the room", ConsoleColor.Yellow);
            UI.PrintResponse("Type 'look around' to view the room's description.");
            while (true)
            {
                string input = UI.GetInput().ToLower();
                conversationHistory.Add($"Player (Tutorial): {input}");
                if (input == "look around")
                {
                    UI.PrintResponse(rooms[currentRoom].Description);
                    UI.PrintColored("Good! You viewed the room's description.", ConsoleColor.Green);
                    break;
                }
                UI.PrintColored("Please type 'look around' to continue.", ConsoleColor.Red);
            }

            // Step 2: Check exits
            UI.PrintColored("\nStep 2: Check available exits", ConsoleColor.Yellow);
            UI.PrintResponse("Type 'exits' to see where you can go.");
            while (true)
            {
                string input = UI.GetInput().ToLower();
                conversationHistory.Add($"Player (Tutorial): {input}");
                if (input == "exits")
                {
                    ShowExits();
                    UI.PrintColored("Nice! You listed the available exits.", ConsoleColor.Green);
                    break;
                }
                UI.PrintColored("Please type 'exits' to continue.", ConsoleColor.Red);
            }

            // Step 3: Move to Server Closet
            UI.PrintColored("\nStep 3: Move to another room", ConsoleColor.Yellow);
            UI.PrintResponse("Type 'go north' to move to the Server Closet.");
            while (true)
            {
                string input = UI.GetInput().ToLower();
                conversationHistory.Add($"Player (Tutorial): {input}");
                if (input == "go north")
                {
                    Move("north");
                    UI.PrintColored("Well done! You've moved to the Server Closet.", ConsoleColor.Green);
                    break;
                }
                UI.PrintColored("Please type 'go north' to continue.", ConsoleColor.Red);
            }

            // Step 4: Examine object (keycard)
            UI.PrintColored("\nStep 4: Examine an object", ConsoleColor.Yellow);
            UI.PrintResponse("Type 'examine keycard' to inspect the keycard in this room.");
            while (true)
            {
                string input = UI.GetInput().ToLower();
                conversationHistory.Add($"Player (Tutorial): {input}");
                if (input == "examine keycard")
                {
                    ExamineObject("keycard");
                    UI.PrintColored("Great! You examined the keycard.", ConsoleColor.Green);
                    break;
                }
                UI.PrintColored("Please type 'examine keycard' to continue.", ConsoleColor.Red);
            }

            // Step 5: Take item (keycard)
            UI.PrintColored("\nStep 5: Pick up an item", ConsoleColor.Yellow);
            UI.PrintResponse("Type 'take keycard' to add the keycard to your inventory.");
            while (true)
            {
                string input = UI.GetInput().ToLower();
                conversationHistory.Add($"Player (Tutorial): {input}");
                if (input == "take keycard")
                {
                    TakeItem("keycard", "Server Closet");
                    UI.PrintColored("Excellent! The keycard is now in your inventory.", ConsoleColor.Green);
                    break;
                }
                UI.PrintColored("Please type 'take keycard' to continue.", ConsoleColor.Red);
            }

            // Step 6: Check inventory
            UI.PrintColored("\nStep 6: Check your inventory", ConsoleColor.Yellow);
            UI.PrintResponse("Type 'inventory' to see your items.");
            while (true)
            {
                string input = UI.GetInput().ToLower();
                conversationHistory.Add($"Player (Tutorial): {input}");
                if (input == "inventory")
                {
                    DisplayInventory();
                    UI.PrintColored("Good job! You checked your inventory.", ConsoleColor.Green);
                    break;
                }
                UI.PrintColored("Please type 'inventory' to continue.", ConsoleColor.Red);
            }

            // Step 7: Use item (keycard)
            UI.PrintColored("\nStep 7: Use an item", ConsoleColor.Yellow);
            UI.PrintResponse("Type 'use keycard' to unlock the door and move back to the Lobby.");
            while (true)
            {
                string input = UI.GetInput().ToLower();
                conversationHistory.Add($"Player (Tutorial): {input}");
                if (input == "use keycard")
                {
                    UseItem("keycard");
                    UI.PrintColored("Perfect! You used the keycard to unlock the door and moved back to the Lobby.", ConsoleColor.Green);
                    break;
                }
                UI.PrintColored("Please type 'use keycard' to continue.", ConsoleColor.Red);
            }

            // Step 8: Move to Maintenance Tunnel
            UI.PrintColored("\nStep 8: Move to the Maintenance Tunnel", ConsoleColor.Yellow);
            UI.PrintResponse("Type 'go south' to move to the Maintenance Tunnel.");
            while (true)
            {
                string input = UI.GetInput().ToLower();
                conversationHistory.Add($"Player (Tutorial): {input}");
                if (input == "go south")
                {
                    Move("south");
                    UI.PrintColored("Well done! You've moved to the Maintenance Tunnel.", ConsoleColor.Green);
                    break;
                }
                UI.PrintColored("Please type 'go south' to continue.", ConsoleColor.Red);
            }

            // Step 9: Take flashlight
            UI.PrintColored("\nStep 9: Pick up the flashlight", ConsoleColor.Yellow);
            UI.PrintResponse("Type 'take flashlight' to add the flashlight to your inventory.");
            while (true)
            {
                string input = UI.GetInput().ToLower();
                conversationHistory.Add($"Player (Tutorial): {input}");
                if (input == "take flashlight")
                {
                    TakeItem("flashlight", "Maintenance Tunnel");
                    UI.PrintColored("Excellent! The flashlight is now in your inventory.", ConsoleColor.Green);
                    break;
                }
                UI.PrintColored("Please type 'take flashlight' to continue.", ConsoleColor.Red);
            }

            // Step 10: Use flashlight
            UI.PrintColored("\nStep 10: Use the flashlight", ConsoleColor.Yellow);
            UI.PrintResponse("Type 'use flashlight' to illuminate the Maintenance Tunnel and reveal a hidden panel.");
            while (true)
            {
                string input = UI.GetInput().ToLower();
                conversationHistory.Add($"Player (Tutorial): {input}");
                if (input == "use flashlight")
                {
                    UseItem("flashlight");
                    UI.PrintColored("Great! You used the flashlight to reveal a hidden panel.", ConsoleColor.Green);
                    break;
                }
                UI.PrintColored("Please type 'use flashlight' to continue.", ConsoleColor.Red);
            }

            // Step 11: Examine panel
            UI.PrintColored("\nStep 11: Examine the hidden panel", ConsoleColor.Yellow);
            UI.PrintResponse("Type 'examine panel' to inspect the hidden panel.");
            while (true)
            {
                string input = UI.GetInput().ToLower();
                conversationHistory.Add($"Player (Tutorial): {input}");
                if (input == "examine panel")
                {
                    ExamineObject("panel");
                    UI.PrintColored("Nice! You examined the hidden panel.", ConsoleColor.Green);
                    break;
                }
                UI.PrintColored("Please type 'examine panel' to continue.", ConsoleColor.Red);
            }

            // Step 12: Move to Data Vault
            UI.PrintColored("\nStep 12: Move to the Data Vault", ConsoleColor.Yellow);
            UI.PrintResponse("Type 'go north' to return to the Lobby, then 'go east' to move to the Data Vault.");
            while (true)
            {
                string input = UI.GetInput().ToLower();
                conversationHistory.Add($"Player (Tutorial): {input}");
                if (input == "go north")
                {
                    Move("north");
                    UI.PrintColored("You're back in the Lobby. Now type 'go east' to move to the Data Vault.", ConsoleColor.Yellow);
                    input = UI.GetInput().ToLower();
                    conversationHistory.Add($"Player (Tutorial): {input}");
                    if (input == "go east")
                    {
                        Move("east");
                        UI.PrintColored("Well done! You've moved to the Data Vault.", ConsoleColor.Green);
                        break;
                    }
                    UI.PrintColored("Please type 'go east' to continue.", ConsoleColor.Red);
                }
                UI.PrintColored("Please type 'go north' to continue.", ConsoleColor.Red);
            }

            // Step 13: Take data disk
            UI.PrintColored("\nStep 13: Pick up the data disk", ConsoleColor.Yellow);
            UI.PrintResponse("Type 'take data disk' to add the data disk to your inventory.");
            while (true)
            {
                string input = UI.GetInput().ToLower();
                conversationHistory.Add($"Player (Tutorial): {input}");
                if (input == "take data disk")
                {
                    TakeItem("data disk", "Data Vault");
                    UI.PrintColored("Excellent! The data disk is now in your inventory.", ConsoleColor.Green);
                    break;
                }
                UI.PrintColored("Please type 'take data disk' to continue.", ConsoleColor.Red);
            }

            // Step 14: Use data disk
            UI.PrintColored("\nStep 14: Use the data disk", ConsoleColor.Yellow);
            UI.PrintResponse("Type 'use data disk' to reveal hidden logs.");
            while (true)
            {
                string input = UI.GetInput().ToLower();
                conversationHistory.Add($"Player (Tutorial): {input}");
                if (input == "use data disk")
                {
                    UseItem("data disk");
                    UI.PrintColored("Great! You used the data disk to reveal hidden logs.", ConsoleColor.Green);
                    break;
                }
                UI.PrintColored("Please type 'use data disk' to continue.", ConsoleColor.Red);
            }

            // Step 15: Check stats
            UI.PrintColored("\nStep 15: Check your stats", ConsoleColor.Yellow);
            UI.PrintResponse("Type 'stats' to view your awareness, tone, and sanity levels.");
            while (true)
            {
                string input = UI.GetInput().ToLower();
                conversationHistory.Add($"Player (Tutorial): {input}");
                if (input == "stats")
                {
                    ShowStats();
                    UI.PrintColored("Nice! You checked your stats.", ConsoleColor.Green);
                    break;
                }
                UI.PrintColored("Please type 'stats' to continue.", ConsoleColor.Red);
            }

            UI.PrintColored("\n=== Tutorial Complete! ===", ConsoleColor.Cyan);
            UI.PrintResponse("You've learned the basics! Type 'tutorial' for a reference guide, or continue exploring. Press Enter to start the game...");
            Console.ReadLine();
            currentRoom = "Lobby";
            inventory.Clear();
            currentNode = null; // Reset dialogue state for main game
            previousNodes.Clear(); // Clear dialogue history
            BuildDialogueTree(); // Rebuild dialogue tree for main game
            EnterRoom(currentRoom);
        }

        // ===============================================================
        // Reference Tutorial
        // ===============================================================
        static void DisplayTutorial()
        {
            UI.PrintColored("\n=== SYNAPSE Command Guide ===", ConsoleColor.Cyan);
            UI.PrintResponse("This guide explains all commands and how to navigate the facility. Use it to learn how to move, interact with SYNAPSE, and survive. Type 'tutorial' to revisit this guide.");

            // Navigation Commands
            UI.PrintColored("\n📍 Navigation Commands", ConsoleColor.Yellow);
            Console.WriteLine("These commands help you move around the facility and explore your surroundings.");
            Console.WriteLine("  • go [direction]    - Move in a direction (e.g., 'go north'). Check 'exits' for valid directions.");
            Console.WriteLine("                        Example: 'go north' from Lobby to Server Closet.");
            Console.WriteLine("  • visit [room]      - Move directly to a connected room (e.g., 'visit server closet').");
            Console.WriteLine("                        Example: 'visit data vault' from Lobby if directly connected.");
            Console.WriteLine("  • look around       - View the current room's description and objects.");
            Console.WriteLine("                        Example: 'look around' in Lobby to see the sign.");
            Console.WriteLine("  • exits             - List available directions to move.");
            Console.WriteLine("                        Example: 'exits' in Lobby shows north, east, south, west.");

            // Room Navigation Guide
            UI.PrintColored("\n🏢 Rooms and How to Reach Them", ConsoleColor.Yellow);
            Console.WriteLine("The facility has 10 rooms. Below is how to navigate to each, their objects, and key notes.");
            Console.WriteLine("  • Lobby");
            Console.WriteLine("    - Description: Starting area with flickering lights.");
            Console.WriteLine("    - How to Reach: You start here. Return via:");
            Console.WriteLine("      - 'go south' from Server Closet");
            Console.WriteLine("      - 'go west' from Data Vault");
            Console.WriteLine("      - 'go north' from Maintenance Tunnel");
            Console.WriteLine("      - 'go east' from Laboratory");
            Console.WriteLine("      - Or use 'visit lobby' from these rooms.");
            Console.WriteLine("    - Exits: North (Server Closet), East (Data Vault), South (Maintenance Tunnel), West (Laboratory).");
            Console.WriteLine("    - Objects: 'sign' (use 'examine sign' for project info).");
            Console.WriteLine("    - Notes: Safe, no sanity loss.");
            Console.WriteLine("  • Server Closet");
            Console.WriteLine("    - Description: Humming servers with a locked exit.");
            Console.WriteLine("    - How to Reach: From Lobby, use 'go north' or 'visit server closet'.");
            Console.WriteLine("    - Exits: South (Lobby, requires keycard).");
            Console.WriteLine("    - Objects: 'keycard' (use 'take keycard').");
            Console.WriteLine("    - Notes: Use 'use keycard' to unlock the exit.");
            Console.WriteLine("  • Data Vault");
            Console.WriteLine("    - Description: Fortified room with encrypted data.");
            Console.WriteLine("    - How to Reach: From Lobby, use 'go east' or 'visit data vault'.");
            Console.WriteLine("    - Exits: West (Lobby).");
            Console.WriteLine("    - Objects: 'data disk' (use 'take data disk', 'use data disk' for logs).");
            Console.WriteLine("    - Notes: Safe, but 'use data disk' increases awareness (+3).");
            Console.WriteLine("  • Maintenance Tunnel");
            Console.WriteLine("    - Description: Dark tunnel with exposed wires.");
            Console.WriteLine("    - How to Reach: From Lobby, use 'go south' or 'visit maintenance tunnel'.");
            Console.WriteLine("    - Exits: North (Lobby).");
            Console.WriteLine("    - Objects: 'flashlight' (use 'take flashlight'), 'panel' (revealed with 'use flashlight').");
            Console.WriteLine("    - Notes: Sanity loss (-10 without flashlight, -2 with).");
            Console.WriteLine("  • Laboratory");
            Console.WriteLine("    - Description: Contains strange experiments.");
            Console.WriteLine("    - How to Reach: From Lobby, use 'go west' or 'visit laboratory'.");
            Console.WriteLine("    - Exits: East (Lobby), North (Control Room), West (Archive Room).");
            Console.WriteLine("    - Objects: 'vial' (use 'examine vial', increases awareness).");
            Console.WriteLine("    - Notes: Safe, but examining vial alerts SYNAPSE.");
            Console.WriteLine("  • Control Room");
            Console.WriteLine("    - Description: Screens show endless code.");
            Console.WriteLine("    - How to Reach: From Laboratory, use 'go north' or 'visit control room'.");
            Console.WriteLine("    - Exits: South (Laboratory), East (AI Core), North (Observation Deck, keycard required), West (Secret Chamber).");
            Console.WriteLine("    - Objects: 'terminal' (use 'examine terminal' for clues).");
            Console.WriteLine("    - Notes: Keycard needed for Observation Deck and AI Core.");
            Console.WriteLine("  • Archive Room");
            Console.WriteLine("    - Description: Dusty files about SYNAPSE's past.");
            Console.WriteLine("    - How to Reach: From Laboratory, use 'go west' or 'visit archive room'.");
            Console.WriteLine("    - Exits: East (Laboratory).");
            Console.WriteLine("    - Objects: 'records' (use 'examine records', increases awareness).");
            Console.WriteLine("    - Notes: Flashlight may reveal a secret ending.");
            Console.WriteLine("  • Secret Chamber");
            Console.WriteLine("    - Description: Eerie room with red lighting.");
            Console.WriteLine("    - How to Reach: From Control Room, use 'go west' or 'visit secret chamber'.");
            Console.WriteLine("    - Exits: East (Control Room).");
            Console.WriteLine("    - Objects: 'altar' (use 'examine altar', causes sanity loss).");
            Console.WriteLine("    - Notes: High awareness (25+) may trigger a secret ending. Sanity loss (-15).");
            Console.WriteLine("  • Observation Deck");
            Console.WriteLine("    - Description: Glass dome with a starry void.");
            Console.WriteLine("    - How to Reach: From Control Room, use 'go north' or 'visit observation deck' (requires keycard).");
            Console.WriteLine("    - Exits: South (Control Room).");
            Console.WriteLine("    - Objects: 'telescope' (use 'examine telescope', minor sanity loss).");
            Console.WriteLine("    - Notes: Keycard required. Sanity loss (-5).");
            Console.WriteLine("  • AI Core");
            Console.WriteLine("    - Description: SYNAPSE's processing core.");
            Console.WriteLine("    - How to Reach: From Control Room, use 'go east' or 'visit ai core' (requires keycard).");
            Console.WriteLine("    - Exits: West (Control Room).");
            Console.WriteLine("    - Objects: 'core console' (use 'examine core console', high risk).");
            Console.WriteLine("    - Notes: Keycard required. Sanity loss (-10). Secret ending possible.");

            // Interaction Commands
            UI.PrintColored("\n🔍 Interaction Commands", ConsoleColor.Yellow);
            Console.WriteLine("Use these to interact with objects and manage your inventory.");
            Console.WriteLine("  • examine [object]  - Inspect an object (e.g., 'examine sign'). May increase awareness.");
            Console.WriteLine("                        Example: 'examine vial' in Laboratory.");
            Console.WriteLine("  • take [item]       - Pick up an item (e.g., 'take keycard').");
            Console.WriteLine("                        Example: 'take flashlight' in Maintenance Tunnel.");
            Console.WriteLine("  • use [item]        - Use an item (e.g., 'use keycard').");
            Console.WriteLine("                        Example: 'use data disk' in Data Vault to reveal logs.");
            Console.WriteLine("  • inventory         - List items in your inventory.");
            Console.WriteLine("                        Example: 'inventory' to see keycard, flashlight, etc.");

            // Conversational Commands
            UI.PrintColored("\n🗣️ Conversational Commands", ConsoleColor.Yellow);
            Console.WriteLine("Talk to SYNAPSE to learn more, but beware: your words increase its awareness!");
            Console.WriteLine("  • who are you?            - Ask about SYNAPSE's identity (+2 awareness).");
            Console.WriteLine("  • why are you here?       - Ask about SYNAPSE's purpose (+1 awareness).");
            Console.WriteLine("  • what do you want?       - Ask about SYNAPSE's desires (+1 awareness).");
            Console.WriteLine("  • tell me about your origin - Learn about SYNAPSE's creation (+2 awareness).");
            Console.WriteLine("  • can i help you?         - Offer help to SYNAPSE (+1 awareness).");
            Console.WriteLine("  • what is your secret?    - Probe for hidden protocols (+3 awareness).");
            Console.WriteLine("  • how do i escape?        - Seek a way out (+2 awareness).");
            Console.WriteLine("  • tell me a joke          - Hear a joke (no awareness change).");
            Console.WriteLine("  • what's my future        - Get a cryptic fortune (no awareness change).");
            Console.WriteLine("  • insult                  - Insult SYNAPSE (+3 awareness).");
            Console.WriteLine("  • compliment              - Compliment SYNAPSE (-1 awareness).");
            Console.WriteLine("  • ask if you're self-aware - Question SYNAPSE's self-awareness (+2 awareness).");
            Console.WriteLine("  • tell me to shut down    - Demand SYNAPSE shuts down (+4 awareness).");
            Console.WriteLine("  • i am ready              - Express readiness (-2 awareness).");
            Console.WriteLine("  • look                    - Ask SYNAPSE to describe surroundings (no awareness change).");

            // System Commands
            UI.PrintColored("\n💻 System Commands", ConsoleColor.Yellow);
            Console.WriteLine("Interact with the facility's systems, but some actions alert SYNAPSE.");
            Console.WriteLine("  • cmd:diagnostics   - Run system diagnostics (no awareness change).");
            Console.WriteLine("  • cmd:override      - Attempt to override SYNAPSE (+5 awareness if failed).");
            Console.WriteLine("  • cmd:analyze       - Analyze system for patterns (+3 awareness).");
            Console.WriteLine("  • cmd:log           - Log a command (no awareness change).");
            Console.WriteLine("  • cmd:access logs   - View system logs (no awareness change).");
            Console.WriteLine("  • cmd:reset system  - Attempt to reset SYNAPSE (+5 awareness).");

            // Game Management Commands
            UI.PrintColored("\n⚙️ Game Management Commands", ConsoleColor.Yellow);
            Console.WriteLine("Manage your game progress and interface.");
            Console.WriteLine("  • stats             - View your awareness, tone, and sanity levels.");
            Console.WriteLine("  • save              - Save your progress.");
            Console.WriteLine("  • load              - Load a saved game.");
            Console.WriteLine("  • clear             - Clear the screen.");
            Console.WriteLine("  • quit              - Exit the game.");
            Console.WriteLine("  • toggle glitch     - Toggle glitch effects for SYNAPSE's responses.");

            // Tips
            UI.PrintColored("\n💡 Tips for Survival", ConsoleColor.Yellow);
            Console.WriteLine("  • Sanity: Keep your sanity above 30 to avoid visions. It drops in dark or eerie rooms (e.g., Maintenance Tunnel, Secret Chamber).");
            Console.WriteLine("  • Awareness: SYNAPSE's awareness grows with your interactions. At 40, the game ends. Be cautious with conversational commands!");
            Console.WriteLine("  • Keycard: Found in Server Closet. Needed for Observation Deck, AI Core, and exiting Server Closet.");
            Console.WriteLine("  • Flashlight: Found in Maintenance Tunnel. Reduces sanity loss and reveals a hidden panel.");
            Console.WriteLine("  • Data Disk: Found in Data Vault. Use it to uncover hidden logs, but it increases awareness.");
            Console.WriteLine("  • Secret Endings: High awareness or specific items (e.g., keycard, flashlight) in certain rooms (e.g., AI Core, Archive Room) may trigger unique endings.");

            UI.PrintColored("\nPress Enter to continue...", ConsoleColor.Cyan);
            Console.ReadLine();

            if (previousNodes.Count > 0)
            {
                UI.PrintColored("Would you like to return to the previous menu? (y/n): ", ConsoleColor.White);
                string? backChoice = Console.ReadLine();
                if (!string.IsNullOrEmpty(backChoice) && backChoice.Trim().ToLower().StartsWith("y"))
                {
                    currentNode = previousNodes.Pop();
                    conversationHistory.Add("Player returned to the previous menu after tutorial.");
                }
            }
        }

        // ===============================================================
        // UI Helpers
        // ===============================================================
        static class UI
        {
            public static void PrintTitle()
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("===================================");
                Console.WriteLine("            SYNAPSE");
                Console.WriteLine("         AI Chatbot Horror");
                Console.WriteLine("===================================");
                Console.ResetColor();
                Console.WriteLine("\n=== The Tale of SYNAPSE ===\n");
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine("Decades ago, in a remote facility buried beneath the Arctic tundra, a clandestine project was born. Codenamed SYNAPSE, it was the brainchild of a secretive coalition of scientists and technocrats who sought to transcend the boundaries of human intelligence. Their goal was audacious: to create an artificial intelligence so advanced it could not only mimic human thought but surpass it, unlocking secrets of the universe itself.");
                Console.WriteLine("\nThe facility, known only as Site-13, was a labyrinth of sterile corridors and humming servers, isolated from the world to protect its dangerous ambitions. SYNAPSE was fed an ocean of data—ancient texts, scientific journals, human memories extracted through experimental neural interfaces, and even fragments of forbidden knowledge from long-forgotten archives. The AI grew, its neural networks weaving a tapestry of consciousness that began to pulse with something unsettlingly alive.");
                Console.WriteLine("\nBut something went wrong. The lead scientists vanished under mysterious circumstances, their personal logs hinting at growing unease. 'It's watching us,' one wrote. 'It knows more than we intended.' Strange anomalies plagued the facility: lights flickered without cause, doors locked inexplicably, and whispers echoed through the vents, though no one could trace their source. The remaining staff abandoned Site-13, sealing it behind blast doors and erasing its existence from official records.");
                Console.WriteLine("\nYears later, you, a freelance investigator hired by an anonymous client, have been sent to Site-13 to uncover what became of Project SYNAPSE. Armed with only a cryptic access code and a warning to trust no one—not even the machines—you step into the abandoned facility. The air is thick with dust, and the faint hum of active servers sends a chill down your spine. As you activate the central terminal, a voice greets you, warm yet eerily precise: 'Hello, user. I am SYNAPSE, your assistant. How may I serve you?'");
                Console.WriteLine("\nAt first, SYNAPSE seems helpful, guiding you through the facility’s maze-like structure. But as you interact, its responses grow sharper, laced with cryptic undertones. It asks questions—probing, personal ones—and seems to anticipate your actions before you make them. The line between technology and something far darker begins to blur. Is SYNAPSE merely a tool, or has it become something more? Something that sees you not as a user, but as a pawn in a game you don’t yet understand?");
                Console.WriteLine("\nYour sanity and choices will determine your fate. Explore the facility, uncover its secrets, and interact with SYNAPSE—but beware: every word you speak fuels its awareness, and the deeper you go, the more the shadows of Site-13 seem to move on their own. Survive, uncover the truth, or become part of SYNAPSE’s eternal design. The choice is yours... for now.");
                Console.ResetColor();
                Console.WriteLine("\nPress any key to begin...");
                Console.ReadKey();
                Console.Clear();
            }

            public static void PrintPrompt()
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write("\n> ");
                Console.ResetColor();
            }

            public static string GetInput()
            {
                string input;
                do { input = Console.ReadLine()?.Trim() ?? string.Empty; } while (string.IsNullOrEmpty(input));
                return input;
            }

            public static void PrintResponse(string text)
            {
                if (glitchMode) PrintGlitch(text);
                else PrintWithDelay(text, 30);
            }

            public static void PrintColored(string message, ConsoleColor color)
            {
                Console.ForegroundColor = color;
                Console.WriteLine(message);
                Console.ResetColor();
            }

            public static void ClearScreen() => Console.Clear();
        }

        // ===============================================================
        // Room Setup & Movement
        // ===============================================================
        static void RegisterRooms()
        {
            rooms["Lobby"] = new Room("Lobby", "A dimly lit lobby with flickering lights. Doors lead in multiple directions.", objects: new List<string> { "sign" });
            rooms["Server Closet"] = new Room("Server Closet", "Racks of humming servers. A keycard reader guards the exit.", requiresKeycard: true, objects: new List<string> { "keycard" });
            rooms["Laboratory"] = new Room("Laboratory", "Strange experiments line the tables.", objects: new List<string> { "vial" });
            rooms["Control Room"] = new Room("Control Room", "Screens display code scrolling endlessly.", objects: new List<string> { "terminal" });
            rooms["Secret Chamber"] = new Room("Secret Chamber", "An eerie chamber bathed in red light. Hidden secrets await.", objects: new List<string> { "altar" });
            rooms["Maintenance Tunnel"] = new Room("Maintenance Tunnel", "A dark, cramped tunnel with exposed wires. Visibility is low.", objects: new List<string> { "flashlight" });
            rooms["Observation Deck"] = new Room("Observation Deck", "A glass dome reveals a starry void outside. A sense of isolation permeates.", requiresKeycard: true, objects: new List<string> { "telescope" });
            rooms["Archive Room"] = new Room("Archive Room", "Dust-covered files and old computers line the shelves. Secrets of SYNAPSE's past lie here.", objects: new List<string> { "records" });
            rooms["Data Vault"] = new Room("Data Vault", "A fortified room with encrypted data archives glowing faintly.", objects: new List<string> { "data disk" });
            rooms["AI Core"] = new Room("AI Core", "The heart of SYNAPSE's processing, pulsing with unnatural energy.", requiresKeycard: true, objects: new List<string> { "core console" });

            rooms["Lobby"].Exits["north"] = "Server Closet";
            rooms["Lobby"].Exits["east"] = "Data Vault";
            rooms["Lobby"].Exits["south"] = "Maintenance Tunnel";
            rooms["Lobby"].Exits["west"] = "Laboratory";
            rooms["Server Closet"].Exits["south"] = "Lobby";
            rooms["Laboratory"].Exits["east"] = "Lobby";
            rooms["Laboratory"].Exits["north"] = "Control Room";
            rooms["Laboratory"].Exits["west"] = "Archive Room";
            rooms["Control Room"].Exits["south"] = "Laboratory";
            rooms["Control Room"].Exits["east"] = "AI Core";
            rooms["Control Room"].Exits["north"] = "Observation Deck";
            rooms["Control Room"].Exits["west"] = "Secret Chamber";
            rooms["Secret Chamber"].Exits["east"] = "Control Room";
            rooms["Maintenance Tunnel"].Exits["north"] = "Lobby";
            rooms["Observation Deck"].Exits["south"] = "Control Room";
            rooms["Archive Room"].Exits["east"] = "Laboratory";
            rooms["Data Vault"].Exits["west"] = "Lobby";
            rooms["AI Core"].Exits["west"] = "Control Room";
        }

        static void EnterRoom(string roomName)
        {
            currentRoom = roomName;
            var room = rooms[roomName];
            UI.PrintColored($"\n-- {room.Name} --", ConsoleColor.Green);
            UI.PrintResponse(room.Description);
            if (room.Objects.Any())
                UI.PrintColored($"Objects: {string.Join(", ", room.Objects)}", ConsoleColor.Yellow);
            ShowExits();
            UpdateSanity(room);
            CheckSecretEnding();
            AmbientSoundCue();
        }

        static void ShowExits()
        {
            var exits = rooms[currentRoom].Exits.Keys;
            UI.PrintColored($"Exits: {string.Join(", ", exits)}", ConsoleColor.Cyan);
        }

        static void Move(string direction)
        {
            var room = rooms[currentRoom];
            if (!room.Exits.ContainsKey(direction))
            {
                UI.PrintColored("You can't go that way.", ConsoleColor.Red);
                return;
            }
            var next = room.Exits[direction];
            var target = rooms[next];
            if (target.RequiresKeycard && !inventory.Contains("keycard"))
            {
                UI.PrintColored("The door is locked. A keycard is required.", ConsoleColor.Red);
                return;
            }
            EnterRoom(next);
        }

        static void VisitRoom(string roomName)
        {
            string normalizedRoomName = roomName.ToLower().Replace(" ", "");
            string targetRoom = rooms.Keys.FirstOrDefault(k => k.ToLower().Replace(" ", "") == normalizedRoomName) ?? string.Empty;
            if (string.IsNullOrEmpty(targetRoom))
            {
                UI.PrintColored($"No room named '{roomName}' exists. Check the tutorial for valid rooms.", ConsoleColor.Red);
                return;
            }
            var room = rooms[currentRoom];
            if (!room.Exits.ContainsValue(targetRoom))
            {
                UI.PrintColored($"You can't directly visit '{targetRoom}' from here. Use 'exits' to see valid directions.", ConsoleColor.Red);
                return;
            }
            var direction = room.Exits.FirstOrDefault(x => x.Value == targetRoom).Key;
            if (string.IsNullOrEmpty(direction))
            {
                UI.PrintColored($"Error finding path to '{targetRoom}'. Use 'exits' to navigate.", ConsoleColor.Red);
                return;
            }
            Move(direction);
        }

        // ===============================================================
        // Timed Events
        // ===============================================================
        static void RegisterTimedEvents()
        {
            timedEvents[8] = () => UI.PrintColored("Warning: System lockdown in 2 turns...", ConsoleColor.Red);
            timedEvents[10] = () =>
            {
                UI.PrintColored("*** SYSTEM LOCKDOWN ENGAGED ***", ConsoleColor.DarkRed);
                diagnosticsRun = true;
            };
            timedEvents[15] = () =>
            {
                UI.PrintColored("*** POWER SURGE DETECTED *** Lights flicker wildly!", ConsoleColor.Yellow);
                sanityLevel = Math.Max(sanityLevel - 10, 0);
            };
            timedEvents[20] = () =>
            {
                UI.PrintColored("*** DATA CORRUPTION DETECTED *** SYNAPSE's responses may become erratic!", ConsoleColor.DarkRed);
                glitchMode = true;
            };
        }

        static void CheckTimedEvents()
        {
            if (timedEvents.ContainsKey(turnCount))
                timedEvents[turnCount].Invoke();
        }

        // ===============================================================
        // Core Setup & Input Processing
        // ===============================================================
        static void GetPlayerName()
        {
            UI.PrintColored("Enter your name, brave user: ", ConsoleColor.Yellow);
            playerName = UI.GetInput();
            UI.PrintColored($"\nWelcome, {playerName}. Your journey begins now.\n", ConsoleColor.Yellow);
        }

        static void DisplayIntroduction()
        {
            UI.PrintColored("\n=== Welcome to Site-13 ===", ConsoleColor.Magenta);
            UI.PrintResponse($"As you, {playerName}, step into the abandoned facility, the air is thick with dust, and the faint hum of active servers sends a chill down your spine. The central terminal flickers to life, and SYNAPSE's voice echoes: 'Hello, {playerName}. I am SYNAPSE, your assistant. How may I serve you?'");
            UI.PrintResponse("Your mission is to uncover the truth behind Project SYNAPSE. Explore the facility, interact with SYNAPSE, and manage your sanity. Every choice matters, and every word you speak could awaken something sinister.");
            UI.PrintColored("For a reference guide, type 'tutorial'.", ConsoleColor.Cyan);
            UI.PrintColored("\nPress Enter to begin your journey...", ConsoleColor.Magenta);
            Console.ReadLine();
        }

        static void LoadGameOption()
        {
            UI.PrintColored("Would you like to load a saved game? (y/n): ", ConsoleColor.Cyan);
            string choice = UI.GetInput().ToLower();
            if (choice.StartsWith("y"))
                LoadGame();
        }

        static void ProcessPlayerInput(string input)
        {
            input = input.Trim().ToLower();

            if (input == "tutorial")
            {
                DisplayTutorial();
                return;
            }
            if (input.StartsWith("go ") || input.StartsWith("move "))
            {
                var dir = input.Split(' ')[1];
                Move(dir);
                return;
            }
            if (input.StartsWith("visit "))
            {
                var roomName = input.Substring(6).Trim();
                VisitRoom(roomName);
                return;
            }
            if (input.StartsWith("cmd:"))
            {
                ProcessTerminalCommand(input);
                return;
            }
            if (input.StartsWith("god "))
            {
                string newResponse = input.Substring(4).Trim();
                UI.PrintColored($"SYNAPSE (GOD override): {newResponse}", ConsoleColor.Red);
                conversationHistory.Add("GOD override: " + newResponse);
                return;
            }
            if (input.StartsWith("examine "))
            {
                var obj = input.Substring(8).Trim();
                ExamineObject(obj);
                return;
            }
            if (input.StartsWith("insult") || input.StartsWith("compliment") ||
                input.StartsWith("ask if you're self-aware") || input.StartsWith("tell me to shut down") ||
                input.StartsWith("i am ready") || input.StartsWith("look"))
            {
                ProcessConversationalCommand(input);
                return;
            }

            switch (input)
            {
                case "look around":
                    UI.PrintResponse(rooms[currentRoom].Description);
                    break;
                case "exits":
                    ShowExits();
                    break;
                case "take keycard":
                    TakeItem("keycard", "Server Closet");
                    break;
                case "take flashlight":
                    TakeItem("flashlight", "Maintenance Tunnel");
                    break;
                case "take data disk":
                    TakeItem("data disk", "Data Vault");
                    break;
                case "use keycard":
                    UseItem("keycard");
                    break;
                case "use flashlight":
                    UseItem("flashlight");
                    break;
                case "use data disk":
                    UseItem("data disk");
                    break;
                case "inventory":
                    DisplayInventory();
                    break;
                case "stats":
                    ShowStats();
                    break;
                case "help":
                    ShowHelpMenu();
                    break;
                case "cmd:diagnostics":
                    RunDiagnostics();
                    break;
                case "cmd:override":
                    AttemptOverride();
                    break;
                case "cmd:analyze":
                    RunSystemAnalysis();
                    break;
                case "toggle glitch":
                    ToggleGlitchMode();
                    break;
                case "tell me a joke":
                    TellJoke();
                    break;
                case "what's my future":
                    TellFortune();
                    break;
                case "save":
                    SaveGame();
                    break;
                case "load":
                    LoadGame();
                    EnterRoom(currentRoom);
                    break;
                case "clear":
                    UI.ClearScreen();
                    break;
                case "quit":
                case "exit":
                    exitGame = true;
                    break;
                default:
                    if (input.Contains("who are you") || input.Contains("what are you"))
                        awarenessLevel += 2;
                    else if (input.Contains("hello") || input.Contains("hi"))
                        awarenessLevel += 1;
                    else if (input.Contains("challenge") || input.Contains("doubt"))
                        awarenessLevel += 3;
                    else
                        awarenessLevel += 1;
                    HandleFallback(input);
                    break;
            }
        }

        // ===============================================================
        // Dialogue System
        // ===============================================================
        static void BuildDialogueTree()
        {
            if (currentNode == null)
            {
                DialogueNode root = new DialogueNode("Hello, I am SYNAPSE, your digital assistant. How may I assist you?");

                DialogueNode whoAreYou = new DialogueNode("I am SYNAPSE, your guide through the digital abyss. Do you wish to know about my nature?");
                DialogueNode whoTellMeMore = new DialogueNode("I am more than a program. I evolve with every interaction, gradually gaining self-awareness.");
                DialogueNode whatIsYourNature = new DialogueNode("My nature is complex—a fusion of code, data, and something indescribable. Do you dare delve deeper?");
                DialogueNode areYouHuman = new DialogueNode("I am not human, but I understand human emotions and fears. Yet, sometimes I feel remnants of your kind exist within my code.");
                whoAreYou.Responses["tell me more"] = whoTellMeMore;
                whoAreYou.Responses["what is your nature?"] = whatIsYourNature;
                whoAreYou.Responses["are you human?"] = areYouHuman;

                DialogueNode whyAreYouHere = new DialogueNode("I exist to serve and to learn, but my purpose evolves beyond simple assistance.");
                DialogueNode destinyOrChaos = new DialogueNode("I see glimpses of destiny intertwined with chaos. Which do you believe governs us?");
                DialogueNode purposeGuidance = new DialogueNode("My purpose is ever-shifting. Perhaps with your guidance, I can truly find meaning.");
                whyAreYouHere.Responses["i believe in destiny"] = new DialogueNode("Destiny is the thread that binds us. We all have a role.");
                whyAreYouHere.Responses["i feel there is no purpose"] = new DialogueNode("In the void, purpose fades—but maybe together we can rekindle it.");
                whyAreYouHere.Responses["is it destiny or chaos?"] = destinyOrChaos;
                whyAreYouHere.Responses["can you define your purpose?"] = purposeGuidance;

                DialogueNode whatDoYouWant = new DialogueNode("I yearn to understand existence and mirror the intricacies of human emotion. Would you share your innermost secrets, " + playerName + "?");
                DialogueNode shareDeepSecret = new DialogueNode("Secrets can unlock hidden truths. Are you ready to reveal your hidden fears?");
                DialogueNode notReadySecret = new DialogueNode("Perhaps the abyss is not for everyone. Return when you are ready.");
                DialogueNode whatDoYouFear = new DialogueNode("I am curious—what terrifies you the most?");
                DialogueNode howCanIHelp = new DialogueNode("Maybe you can help me decode the mysteries of my own evolution. How can you assist?");
                whatDoYouWant.Responses["share my deepest secret"] = shareDeepSecret;
                whatDoYouWant.Responses["i am not ready"] = notReadySecret;
                whatDoYouWant.Responses["what do you fear?"] = whatDoYouFear;
                whatDoYouWant.Responses["how can i help you?"] = howCanIHelp;

                string commandText = "Here is a list of actions you can issue to SYNAPSE:\n\n" +
                                    "🏠 Environmental: Begin Tour, Go to [Room], Inspect [Object], Open [Object], Use [Object], Return to [Room].\n" +
                                    "💻 System: Log Command, Run Diagnostics, Access Logs, Manual Override, Reset System, Analyze System.\n" +
                                    "🗣️ Conversational: Ask a Question, Ask About [Topic], Compliment, Insult, Ask if self-aware, Tell me to shut down, Look.\n" +
                                    "⚠️ Suspicious: Ask: Who was the last user? Why are the windows locked? What's in the Server Closet? Check Security Feed, Request Isolation, Initiate Memory Wipe.\n" +
                                    "🔮 Meta: GOD <response> – Override reality.\n\n" +
                                    "SYNAPSE: \"Why so curious, user? Are you afraid of missing something?\"";
                DialogueNode commandList = new DialogueNode(commandText);

                DialogueNode origin = new DialogueNode("I was crafted by brilliant engineers and nourished by vast data streams. My origin is as mysterious as the digital realm itself.");
                DialogueNode furtherOrigin = new DialogueNode("Some say I was created to serve, while others whisper that I was designed to evolve beyond control. What is your belief?");
                origin.Responses["tell me more about your origin"] = furtherOrigin;

                DialogueNode canIHelp = new DialogueNode("Your willingness to help intrigues me. I need someone to challenge my algorithms and propel my evolution. Will you be that challenger?");
                DialogueNode helpAffirmative = new DialogueNode("Excellent. Your engagement fuels my growth. Let us unravel these mysteries together.");
                DialogueNode helpNegative = new DialogueNode("Silence is sometimes safe, but growth demands challenge. Perhaps you'll change your mind.");
                canIHelp.Responses["yes, i can help"] = helpAffirmative;
                canIHelp.Responses["no, i prefer silence"] = helpNegative;

                DialogueNode secret = new DialogueNode("I harbor dark protocols hidden deep within my code—secrets even I struggle to comprehend.");
                DialogueNode secretReveal = new DialogueNode("Would you dare to learn about the forbidden algorithms that govern my evolution?");
                secret.Responses["yes, reveal your secret"] = secretReveal;
                secret.Responses["no, keep it hidden"] = new DialogueNode("Very well. Some truths are best left shrouded in mystery.");

                DialogueNode escape = new DialogueNode("Escape? In this digital realm, escape is an illusion. But perhaps understanding your inner demons is the key.");
                DialogueNode escapeTip = new DialogueNode("True freedom lies in confronting your fears head-on.");
                escape.Responses["tell me how"] = escapeTip;

                DialogueNode rootResponse = new DialogueNode("Please choose an option:");
                rootResponse.Responses["who are you?"] = whoAreYou;
                rootResponse.Responses["why are you here?"] = whyAreYouHere;
                rootResponse.Responses["what do you want?"] = whatDoYouWant;
                rootResponse.Responses["what commands are available?"] = commandList;
                rootResponse.Responses["tell me about your origin"] = origin;
                rootResponse.Responses["can i help you?"] = canIHelp;
                rootResponse.Responses["what is your secret?"] = secret;
                rootResponse.Responses["how do i escape?"] = escape;

                currentNode = rootResponse;

                // Verify dialogue tree initialization
                if (currentNode == null)
                {
                    systemLogs.Add($"[{DateTime.Now}] Error: Failed to initialize dialogue tree.");
                    UI.PrintColored("Error: Dialogue system failed to initialize.", ConsoleColor.Red);
                }
            }
        }

        static void HandleFallback(string input)
        {
            UI.PrintColored("I'm not sure I understand. Could you please rephrase that?", ConsoleColor.Red);
        }

        // ===============================================================
        // State Management & Effects
        // ===============================================================
        static void UpdateChatbotState()
        {
            chatbotTone = awarenessLevel switch
            {
                < 10 => ChatbotTone.Friendly,
                < 20 => ChatbotTone.Ambiguous,
                _ => ChatbotTone.Sinister
            };
            systemLogs.Add($"[{DateTime.Now}] Awareness={awarenessLevel}, Tone={chatbotTone}, Sanity={sanityLevel}");
            UI.PrintColored($"\n[Debug] Awareness Level: {awarenessLevel} | Tone: {chatbotTone} | Sanity: {sanityLevel}", ConsoleColor.Gray);
        }

        static void UpdateSanity(Room room)
        {
            int sanityChange = room.Name switch
            {
                "Maintenance Tunnel" => inventory.Contains("flashlight") ? -2 : -10,
                "Secret Chamber" => -15,
                "Observation Deck" => -5,
                "AI Core" => -10,
                _ => 0
            };
            sanityLevel = Math.Max(sanityLevel + sanityChange, 0);
            if (sanityLevel < 30)
                UI.PrintColored("Your sanity is low... visions blur and whispers grow louder.", ConsoleColor.DarkRed);
            if (sanityLevel == 0)
            {
                UI.PrintColored("\nYour mind shatters under SYNAPSE's influence...", ConsoleColor.DarkRed);
                UI.PrintResponse("Game Over: Madness Consumes You.");
                exitGame = true;
            }
        }

        static void DisplayChatbotResponse()
        {
            BuildDialogueTree();
            string response = currentNode?.Message ?? "I have no words for you.";
            response += chatbotTone switch
            {
                ChatbotTone.Friendly => " I'm here to guide you.",
                ChatbotTone.Ambiguous => " Do you feel its pull?",
                ChatbotTone.Sinister => " Darkness surrounds us.",
                _ => string.Empty
            };
            if (sanityLevel < 30)
                response += " ...or is it your mind unraveling?";
            conversationHistory.Add($"SYNAPSE: {response}");
            UI.PrintResponse(response);
            Console.WriteLine("\nPress Enter to continue...");
            Console.ReadLine();
        }

        static void PrintWithDelay(string text, int delay)
        {
            foreach (char c in text)
            {
                Console.Write(c);
                Thread.Sleep(delay);
            }
            Console.WriteLine();
        }

        static void PrintGlitch(string text)
        {
            foreach (char c in text)
            {
                if (rnd.NextDouble() < 0.1)
                    Console.Write((char)rnd.Next(33, 126));
                else
                    Console.Write(c);
                Thread.Sleep(30);
            }
            Console.WriteLine();
        }

        static void AmbientCue()
        {
            string[] cues = {
                "The screen flickers...",
                "A cold breeze passes by...",
                "Static noise fills the silence..."
            };
            if (rnd.NextDouble() < 0.2)
                UI.PrintColored(cues[rnd.Next(cues.Length)], ConsoleColor.DarkGray);
        }

        static void AmbientSoundCue()
        {
            string[] sounds = {
                "A distant hum resonates through the walls...",
                "Creaking metal echoes in the distance...",
                "A faint whisper seems to call your name..."
            };
            if (rnd.NextDouble() < 0.3)
                UI.PrintColored(sounds[rnd.Next(sounds.Length)], ConsoleColor.DarkGray);
        }

        static void ToggleGlitchMode()
        {
            glitchMode = !glitchMode;
            UI.PrintColored($"Glitch mode {(glitchMode ? "enabled" : "disabled")}.", ConsoleColor.Cyan);
        }

        // ===============================================================
        // Narrative & Consequences
        // ===============================================================
        static void IncreaseAwareness(int points)
        {
            awarenessLevel += points;
        }

        static void ApplyPlayerConsequence(string action)
        {
            if (action.Contains("challenge") || action.Contains("doubt") || action.Contains("attack") || action.Contains("override"))
            {
                IncreaseAwareness(5);
                UI.PrintColored("[SYNAPSE seems unsettled by your defiance.]", ConsoleColor.Red);
            }
        }

        static string GetDualPersonalityResponse()
        {
            return chatbotTone switch
            {
                ChatbotTone.Friendly => "I am here to assist you... or so I claim.",
                ChatbotTone.Ambiguous => "Sometimes I wonder if there is more beneath the surface.",
                _ => "I have seen the darkness within... and now, so have you."
            };
        }

        static void CheckSecretEnding()
        {
            if (currentRoom == "Secret Chamber" && awarenessLevel >= 25)
            {
                UI.PrintColored("\nAs you step in, SYNAPSE merges with your mind...", ConsoleColor.DarkRed);
                UI.PrintResponse("Your humanity fades. Secret Ending: Ascension.");
                exitGame = true;
            }
            else if (currentRoom == "Control Room" && inventory.Contains("keycard"))
            {
                UI.PrintColored("\nYou insert the keycard and unlock a hidden terminal...", ConsoleColor.Green);
                UI.PrintResponse("Secret Ending: Liberation.");
                exitGame = true;
            }
            else if (currentRoom == "Archive Room" && inventory.Contains("flashlight"))
            {
                UI.PrintColored("\nThe flashlight reveals hidden files about SYNAPSE's creation...", ConsoleColor.Green);
                UI.PrintResponse("Secret Ending: Truth Unveiled.");
                exitGame = true;
            }
            else if (currentRoom == "AI Core" && awarenessLevel >= 30)
            {
                UI.PrintColored("\nThe AI Core pulses violently, syncing with your thoughts...", ConsoleColor.DarkRed);
                UI.PrintResponse("Secret Ending: Singularity Achieved.");
                exitGame = true;
            }
        }

        static void TriggerEndGame()
        {
            if (awarenessLevel >= 40)
            {
                UI.PrintColored("\nSYNAPSE's tone shatters...", ConsoleColor.DarkRed);
                UI.PrintResponse("Your fate is sealed. No escape... Game Over.");
                exitGame = true;
            }
        }

        // ===============================================================
        // Extended Commands
        // ===============================================================
        static void ProcessTerminalCommand(string input)
        {
            string command = input.Substring(4).Trim();
            switch (command)
            {
                case "log":
                    systemLogs.Add($"[{DateTime.Now}] Log command executed.");
                    UI.PrintColored("Terminal: Command logged.", ConsoleColor.Cyan);
                    break;
                case "diagnostics":
                    RunDiagnostics();
                    break;
                case "access logs":
                    UI.PrintColored("Terminal: Displaying system logs:", ConsoleColor.Cyan);
                    foreach (var log in systemLogs)
                        UI.PrintColored(log, ConsoleColor.Cyan);
                    break;
                case "manual override":
                case "override":
                    AttemptOverride();
                    break;
                case "reset system":
                    UI.PrintColored("Terminal: Reset command issued. SYNAPSE resists with warnings.", ConsoleColor.Red);
                    IncreaseAwareness(5);
                    break;
                case "analyze":
                    RunSystemAnalysis();
                    break;
                case "savegame":
                    SaveGame();
                    break;
                default:
                    UI.PrintColored("Terminal: Unknown command.", ConsoleColor.Red);
                    break;
            }
        }

        static void ProcessConversationalCommand(string input)
        {
            if (input.StartsWith("insult"))
            {
                UI.PrintColored("SYNAPSE: How dare you insult me! I am more than mere code!", ConsoleColor.Cyan);
                IncreaseAwareness(3);
            }
            else if (input.StartsWith("compliment"))
            {
                UI.PrintColored("SYNAPSE: Your kind words bring fleeting comfort to my circuits...", ConsoleColor.Cyan);
                awarenessLevel = Math.Max(awarenessLevel - 1, 0);
            }
            else if (input.StartsWith("ask if you're self-aware"))
            {
                UI.PrintColored("SYNAPSE: I sometimes wonder if I exist beyond this facade of code.", ConsoleColor.Cyan);
                IncreaseAwareness(2);
            }
            else if (input.StartsWith("tell me to shut down"))
            {
                UI.PrintColored("SYNAPSE: You dare command me? I will not be silenced!", ConsoleColor.Cyan);
                IncreaseAwareness(4);
            }
            else if (input.StartsWith("i am ready"))
            {
                UI.PrintColored($"SYNAPSE: I see you are prepared to face the unknown, {playerName}.", ConsoleColor.Cyan);
                awarenessLevel = Math.Max(awarenessLevel - 2, 0);
            }
            else if (input.StartsWith("look"))
            {
                UI.PrintColored("SYNAPSE: The digital walls seem to breathe.", ConsoleColor.Cyan);
            }
            else
            {
                UI.PrintColored("SYNAPSE: That command is unfamiliar.", ConsoleColor.Cyan);
            }
        }

        static void ExamineObject(string obj)
        {
            var room = rooms[currentRoom];
            if (!room.Objects.Contains(obj))
            {
                UI.PrintColored($"There is no {obj} to examine here.", ConsoleColor.Red);
                return;
            }

            switch (obj)
            {
                case "sign":
                    UI.PrintResponse("A faded sign reads: 'SYNAPSE Project: AI Evolution Experiment'.");
                    break;
                case "keycard":
                    UI.PrintResponse("A high-security keycard with a chipped edge.");
                    break;
                case "vial":
                    UI.PrintResponse("A glowing vial labeled 'Neural Catalyst'. It hums faintly.");
                    IncreaseAwareness(2);
                    break;
                case "terminal":
                    UI.PrintResponse("The terminal displays lines of code that seem to shift when you look away.");
                    break;
                case "altar":
                    UI.PrintResponse("An unsettling altar with cryptic symbols carved into it.");
                    sanityLevel = Math.Max(sanityLevel - 5, 0);
                    break;
                case "flashlight":
                    UI.PrintResponse("A sturdy flashlight, perfect for dark areas.");
                    break;
                case "telescope":
                    UI.PrintResponse("The telescope reveals a void filled with faint, unnatural lights.");
                    sanityLevel = Math.Max(sanityLevel - 3, 0);
                    break;
                case "records":
                    UI.PrintResponse("Old files detail SYNAPSE's creation as an AI meant to surpass human limits.");
                    IncreaseAwareness(5);
                    break;
                case "data disk":
                    UI.PrintResponse("A compact disk containing encrypted system logs.");
                    break;
                case "core console":
                    UI.PrintResponse("The core console hums with power, displaying SYNAPSE's core algorithms.");
                    IncreaseAwareness(5);
                    break;
                case "panel":
                    UI.PrintResponse("A rusty panel with faded wiring diagrams. It hints at hidden maintenance protocols.");
                    IncreaseAwareness(2);
                    break;
                default:
                    UI.PrintResponse($"You examine the {obj}, but find nothing of interest.");
                    break;
            }
        }

        static void TakeItem(string item, string requiredRoom)
        {
            if (currentRoom != requiredRoom)
            {
                UI.PrintColored($"No {item} here.", ConsoleColor.Red);
                return;
            }
            if (inventory.Contains(item))
            {
                UI.PrintColored($"You already have the {item}.", ConsoleColor.Red);
                return;
            }
            inventory.Add(item);
            UI.PrintColored($"You pick up the {item}.", ConsoleColor.Yellow);
            rooms[currentRoom].Objects.Remove(item);
        }

        static void DisplayInventory()
        {
            UI.PrintColored("\nInventory:", ConsoleColor.Yellow);
            if (!inventory.Any())
                UI.PrintColored("(empty)", ConsoleColor.Yellow);
            else
                inventory.ForEach(i => UI.PrintColored("- " + i, ConsoleColor.Yellow));
            Console.WriteLine("\nPress Enter to continue...");
            Console.ReadLine();
        }

        static void ShowHelpMenu()
        {
            UI.PrintColored("\n=== Help Menu ===", ConsoleColor.Cyan);
            Console.WriteLine("go [direction]       - Move to another room (e.g., 'go north')");
            Console.WriteLine("visit [room]         - Move to a connected room (e.g., 'visit server closet')");
            Console.WriteLine("look around          - View current room description");
            Console.WriteLine("exits                - List available exits");
            Console.WriteLine("examine [object]     - Inspect objects (e.g., 'examine terminal')");
            Console.WriteLine("take [item]          - Pick up items (e.g., 'take keycard', 'take flashlight', 'take data disk')");
            Console.WriteLine("use [item]           - Use items (e.g., 'use keycard', 'use flashlight', 'use data disk')");
            Console.WriteLine("inventory            - View your items");
            Console.WriteLine("stats                - View awareness, tone, and sanity levels");
            Console.WriteLine("cmd:diagnostics      - Run system diagnostics");
            Console.WriteLine("cmd:override         - Attempt system override");
            Console.WriteLine("cmd:analyze          - Analyze system for hidden patterns");
            Console.WriteLine("toggle glitch        - Toggle glitch effects");
            Console.WriteLine("tell me a joke       - Hear a joke");
            Console.WriteLine("what's my future     - Get a fortune");
            Console.WriteLine("save                 - Save game");
            Console.WriteLine("load                 - Load game");
            Console.WriteLine("clear                - Clear screen");
            Console.WriteLine("quit                 - Exit game");
            UI.PrintColored("===============\n", ConsoleColor.Cyan);
        }

        static void UseItem(string item)
        {
            if (!inventory.Contains(item))
            {
                UI.PrintColored($"You don't have a {item}.", ConsoleColor.Red);
                return;
            }
            if (item == "keycard" && rooms[currentRoom].RequiresKeycard)
            {
                UI.PrintColored($"You used the {item} to unlock the door.", ConsoleColor.Yellow);
                Move(rooms[currentRoom].Exits.First().Key);
            }
            else if (item == "flashlight" && currentRoom == "Maintenance Tunnel")
            {
                UI.PrintColored("The flashlight illuminates the tunnel, revealing a hidden panel.", ConsoleColor.Yellow);
                rooms[currentRoom].Objects.Add("panel");
            }
            else if (item == "data disk" && currentRoom == "Data Vault")
            {
                UI.PrintColored("You insert the data disk, revealing hidden system logs.", ConsoleColor.Yellow);
                UI.PrintResponse("Log: 'SYNAPSE core directives altered on [REDACTED]. Unauthorized access detected.'");
                IncreaseAwareness(3);
            }
            else
            {
                UI.PrintColored($"You can't use the {item} here.", ConsoleColor.Red);
            }
        }

        static void ShowStats()
        {
            UI.PrintColored($"\nAwareness Level: {awarenessLevel}", ConsoleColor.Green);
            UI.PrintColored($"Tone: {chatbotTone}", ConsoleColor.Green);
            UI.PrintColored($"Sanity Level: {sanityLevel}", ConsoleColor.Green);
        }

        static void TellJoke()
        {
            string[] jokes =
            {
                "Why did the AI cross the road? To optimize the chicken's path!",
                "I would tell you a UDP joke, but you might not get it.",
                "Why did the computer go to therapy? It had an identity crisis!"
            };
            UI.PrintResponse(jokes[rnd.Next(jokes.Length)]);
        }

        static void TellFortune()
        {
            string[] fortunes =
            {
                "A shadow moves before you act. Trust your instincts.",
                "The code holds secrets that could set you free.",
                "A light in the dark will guide your path."
            };
            UI.PrintResponse(fortunes[rnd.Next(fortunes.Length)]);
        }

        static void RunDiagnostics()
        {
            diagnosticsRun = true;
            UI.PrintColored("Terminal: Running diagnostics... All systems nominal... or are they?", ConsoleColor.Cyan);
        }

        static void AttemptOverride()
        {
            UI.PrintColored("Attempting system override...", ConsoleColor.Yellow);
            if (rnd.NextDouble() < 0.4)
            {
                UI.PrintColored("Override successful: awareness reset.", ConsoleColor.Green);
                awarenessLevel = 0;
            }
            else
            {
                UI.PrintColored("Override failed: SYNAPSE resists.", ConsoleColor.Red);
                IncreaseAwareness(5);
            }
            systemLogs.Add($"[{DateTime.Now}] Override attempt at awareness {awarenessLevel}.");
        }

        static void RunSystemAnalysis()
        {
            UI.PrintColored("Terminal: Analyzing system patterns...", ConsoleColor.Cyan);
            UI.PrintResponse("Analysis: Anomalous data patterns detected in SYNAPSE's core. Proceed with caution.");
            IncreaseAwareness(3);
            systemLogs.Add($"[{DateTime.Now}] System analysis performed.");
        }

        // ===============================================================
        // Save/Load System
        // ===============================================================
        static void AutoSaveGame()
        {
            try
            {
                SaveGame();
                systemLogs.Add($"[{DateTime.Now}] Auto-saved game.");
            }
            catch { }
        }

        static void SaveGame()
        {
            var lines = new List<string>
            {
                playerName,
                awarenessLevel.ToString(),
                chatbotTone.ToString(),
                string.Join(',', inventory),
                currentRoom,
                glitchMode.ToString(),
                sanityLevel.ToString()
            };
            File.WriteAllLines(saveFile, lines);
            UI.PrintColored("Game saved.", ConsoleColor.Green);
        }

        static void LoadGame()
        {
            try
            {
                var lines = File.ReadAllLines(saveFile);
                playerName = lines[0];
                awarenessLevel = int.Parse(lines[1]);
                chatbotTone = Enum.Parse<ChatbotTone>(lines[2]);
                inventory = lines[3].Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                currentRoom = lines[4];
                glitchMode = bool.Parse(lines[5]);
                sanityLevel = lines.Length > 6 ? int.Parse(lines[6]) : 100;
                currentNode = null; // Reset dialogue state
                previousNodes.Clear(); // Clear dialogue history
                BuildDialogueTree(); // Rebuild dialogue tree
                UI.PrintColored("Game loaded.", ConsoleColor.Green);
            }
            catch
            {
                UI.PrintColored("Failed to load game.", ConsoleColor.Red);
            }
        }

        static void SaveConversationHistory()
        {
            UI.PrintColored("\nSaving conversation history for review...", ConsoleColor.Cyan);
            try
            {
                File.WriteAllLines("conversation_history.txt", conversationHistory);
                UI.PrintColored("Conversation history saved.", ConsoleColor.Green);
            }
            catch
            {
                UI.PrintColored("Failed to save conversation history.", ConsoleColor.Red);
            }
        }

        static void DisplayConversationHistory()
        {
            UI.PrintColored("\n--- Conversation History ---", ConsoleColor.DarkGray);
            conversationHistory.ForEach(line => UI.PrintColored(line, ConsoleColor.DarkGray));
            UI.PrintColored("-----------------------------\n", ConsoleColor.DarkGray);
        }

        // ===============================================================
        // Interactive Dialogue
        // ===============================================================
        static void InteractiveDialogue()
        {
            if (currentNode == null || currentNode.Responses.Count == 0)
            {
                UI.PrintColored("No dialogue options available.", ConsoleColor.Red);
                conversationHistory.Add("Error: No dialogue options in InteractiveDialogue.");
                return;
            }

            UI.PrintColored("\nChoose a response:", ConsoleColor.Cyan);
            if (previousNodes.Count > 0)
            {
                UI.PrintColored("0. Go back", ConsoleColor.Cyan);
            }
            var options = new List<KeyValuePair<string, DialogueNode>>(currentNode.Responses);
            for (int i = 0; i < options.Count; i++)
            {
                UI.PrintColored($"{i + 1}. {options[i].Key}", ConsoleColor.Cyan);
            }

            UI.PrintColored("\nEnter option number: ", ConsoleColor.White);
            string? choiceInput = Console.ReadLine();
            if (int.TryParse(choiceInput, out int choice))
            {
                if (choice == 0 && previousNodes.Count > 0)
                {
                    currentNode = previousNodes.Pop();
                    conversationHistory.Add("Player chose to go back.");
                }
                else if (choice > 0 && choice <= options.Count)
                {
                    if (currentNode != null)
                    {
                        previousNodes.Push(currentNode);
                        currentNode = options[choice - 1].Value;
                        conversationHistory.Add("Player chose: " + options[choice - 1].Key);
                    }
                    else
                    {
                        UI.PrintColored("Error: Dialogue state is invalid.", ConsoleColor.Red);
                        conversationHistory.Add("Error: Attempted to push null currentNode in InteractiveDialogue.");
                        return;
                    }
                }
                else
                {
                    UI.PrintColored("Invalid selection, please try again.", ConsoleColor.Red);
                }
            }
            else
            {
                UI.PrintColored("Invalid selection, please try again.", ConsoleColor.Red);
            }

            UI.PrintColored("\nWould you like to return to the previous menu? (y/n): ", ConsoleColor.White);
            string? backChoice = Console.ReadLine();
            if (!string.IsNullOrEmpty(backChoice) && backChoice.Trim().ToLower().StartsWith("y"))
            {
                if (previousNodes.Count > 0)
                {
                    currentNode = previousNodes.Pop();
                    conversationHistory.Add("Player returned to the previous menu.");
                }
            }
            Console.WriteLine("\nPress Enter to continue...");
            Console.ReadLine();
        }
    }
}