I want to create an online multiplayer card game, caled SobeSobe. It's a game I used to play with some friends and would like to make it available online to play with them.

# Game Rules and Flow description
- The game can be played by 2-5 players, although ideally 5 players is the recommended number. 
- The game uses a standard deck of 52 playing cards, but you remove the 8's 9's and 10's from the deck, leaving you with 40 cards.
- The value or order of the cards from highest to lowest is as follows: Ace, 7, King, Queen, Jack, 6, 5, 4, 3, 2. (fyi In PT-pt the card 7 is called 'Manilha').
- In the beginning of the game each player starts with 20 points, and the objective of the game is to reach 0 points first.
- The way players decreases points, is by making tricks.
- Each trick can be valued as 1, 2 or 4 points. The description on how we value the tricks will come later.
- In the begining of each round, a player is designated as the dealer, and the dealer shuffles the deck.
- The dealer always deals cards counter-clockwise, starting with the player to his right.
- The dealer is the last one to receive cards.
- The player to the right of the dealer is the 'party player' and this player decides what is the trump suit for that round.
- The trump suit is the suit that will beat all other suits in that round.
- The dealer will first deal 2 cards to each player, counter-clockwise, starting with the player to his right (party player).
- The party player can decide the trump suit before any cards are dealt or after it has received 2 cards from the dealer.
- Any trump can be choosen before dealing cards. (if it's Hearts, in PT-pt this is playfully called 'Copas às escuras').
- Depending on the chosen trump suit and when it was choosen, the value of the tricks will be determined as follows:
  - Hearts values tricks at 2 points.
  - Diamonds, Clubs or Spades value tricks at 1 point.
  - If the trump suit is choosen before dealing the cards, the trick values are doubled.
- After the trump is choosen, each player decides if they want to play that round or not.
- There are some rules for the decision of playing the round or not:
  - The party player must be playing that round.
  - No player can stay out of a round more than 2 consecutive rounds.
  - If the trump is Clubs, all players must play that round.
  - If the player has 5 points or less, he must play that round.
- The ones that play the round receive 3 more cards from the dealer, so they end up with 5 cards.
- Then, in a counter-clockwise order, each player active in the round, can switch up to 3 cards.
- The first player to switch cards is always the party player (the one to the right of the dealer), and then it continues in counter-clockwise order.
- The dealer is the last one to switch cards.
- After switching cards, the trick taking begins.
- The trump card beats any other suit card.
- Players must follow suit if they can.
- If a player cannot follow suit, he must play a trump card if he has one (this is called cut, or cortar in PT-pt).
- If a player cannot follow suit and does not have a trump card, he can play any card.
- If a player has the ace of the trump suit, he must play it as soon as he can (except if it's to do a cut play).
- When a trick is started by a trump suit card, all players must play a higher trump card then the one played previouly, if they can.
- The winner of each trick is the player who played the highest card of the suit led, unless a trump card was played, in which case the highest trump card wins.
- The winner of each trick leads the next trick.
- At the end of the round, the players that played count how many tricks they won, and decrease their points accordingly to the trick values determined at the begining of the round.
- The player that does not play that round does not decrease points.
- The players that played in the round and made no tricks, increase their points:
  - If the trick value is 1 point, they increase 5 points.
  - If the trick value is 2 points, they increase 10 points.
  - If the trick value is 4 points, they increase 20 points.
  - IF the party player makes no tricks, the increase value is doubled.
- The dealer position rotates counter-clockwise after each round.
- The first player to reach 0 points wins the game.
  - Each point that the other players have left, counts as 5 cents of Euros towards the winner.
- The point registration UI should be a simpe table where:
  - Each player is represented by a column.
  - Each round is represented by a row.
  - Starting points is 20 for each player.
  - Each cell is filled with the points after that round.
  - If a player did not play the round, a '-' is placed in that cell. It can also be grayed out to indicate that the player did not play that round.

# Technology Stack
- Frontend: Angular app with best practices for responsive design and UI design (like tailwind or somethig similar).
- Backend: .NET 10 minimal API for handling game logic and real-time communication. EFCore as an ORM to abstract BD layer
- Database: SQLLite for storing user data, game states, and scores (can later be migrated to a different DB engine if needed).
- Real-time Communication: gRPC for real-time multiplayer interactions.
- Use Aspire to orchestrate the deployment and hosting of the application.
- Authentication: Use OAuth 2.0 and OpenID Connect for secure user authentication and authorization.
- Use GitHub for version control and CI/CD pipelines.

# Development Steps
1. **Game Design Document**:
    - Create a detailed game design document outlining the rules, mechanics, and flow of SobeSobe.
    - Define player interactions, scoring system, and game progression.
2. **Wireframing and Prototyping**:
    - Design wireframes for the game interface, including the game board, player hands, and score display.
    - Create a clickable prototype to visualize user interactions and game flow.
3. **Develop the Frontend**:
    - Create the user interface for the game, including the game board, player hands, and score display.
    - Implement animations and transitions for a better user experience.
    - Ensure the UI is responsive and works well on different devices.
4. **Develop the Backend**:
    - Set up the server to handle game logic, player actions, and game state management.
    - Implement user authentication and session management.
    - Create APIs for frontend-backend communication.
    - Integrate gRPC for real-time updates between players.
5. **Database Integration**:
    - Design the database schema to store user profiles, game states, and scores.
    - Implement CRUD operations for managing game data.
6. **Testing**:
    - Conduct unit tests for individual components and functions.
    - Perform integration tests to ensure different parts of the application work together.
7. **Deployment**:
    - use Azure to deploy your application. As simple as possible, with minimal configuration.
    - Set up the necessary infrastructure for hosting the frontend and backend. Ideally with bicep scripts to automate the deployment.
    - Set up continuous integration/continuous deployment (CI/CD) pipelines for smooth updates.
    - Monitor the application for performance and errors.