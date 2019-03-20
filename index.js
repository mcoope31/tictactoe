var http = require('http').Server();
var io = require('socket.io')(http);

http.listen(4000, () => {
  console.log("--- Server Running on Port 4000 ---");
})

var players = {};
var games = {};
var gameCount = 0;

io.on('connection', function(socket) {
  console.log("Someone has connected ",socket.id);

  socket.on('chatMessage', (payload) => {
    console.log("Recieved message from "+payload.name+": "+payload.message);
    io.emit('chatMessage', payload);
  })

  socket.on('join', (payload) => {
    let name = payload[0];
    console.log("Player "+name+" has joined.");

    players[socket.id] = name;

    socket.emit('players', { players });
    socket.broadcast.emit('playerJoined', { id: socket.id, name });
  })

  socket.on('disconnect', () => {
    if(players.hasOwnProperty(socket.id)) {
      console.log("Player "+players[socket.id]+" has left.");
      io.emit('playerLeft', { id: socket.id });
      delete players[socket.id];
    }
  })

  socket.on("playGame", () => {

    for(var key in games) {
      let game = games[key];
      if(!game.started) {
        console.log("Player has joined game "+game.gameId+". Starting game!");
        game.started = true;
        game.oPlayerId = socket.id;
        io.emit("startGame", { xPlayerId: game.xPlayerId, oPlayerId: game.oPlayerId, gameId: game.gameId });
        return;
      }
    }

    let game = {
      gameId: String(gameCount),
      started: false,
      xPlayerId: socket.id,
      board: [-1,-1,-1,-1,-1,-1,-1,-1,-1],
      numTurns: 0,
    }
    console.log("New game created "+game.gameId+", waiting for other player.");

    games[game.gameId] = game;
    gameCount++;
  })

  socket.on("takeTurn", (payload) => {
    let game = games[payload.gameId];
    game.numTurns++;
    let board = game.board;
    board[payload.space] = payload.pieceId;
    let checkWinner = checkVictory(board);

    let data = {
      gameId: game.gameId,
      space: payload.space,
      pieceId: payload.pieceId,
    }

    if(checkWinner) {
      console.log("Game "+game.gameId+" has a winner!");
      data.winner = checkWinner.winner;
      data.winInfo = checkWinner.winInfo;
      io.emit("gameFinish", data);
    } else if(game.numTurns === 9){
      console.log("Game "+game.gameId+" is a tie!");
      data.winner = -1;
      io.emit("gameFinish", data);
    } else {
      console.log("Game "+game.gameId+": Piece "+data.pieceId+" added to space "+data.space);
      io.emit("takeTurn", data);
    }
  })

  //Function to check a game board to see if anyone anyone
  //Return: obj( Int(winner by piece id), String(win type description))
  function checkVictory(board) {

    for(var i = 0; i < 3; i++) {
      //Check all 3 rows for winner
      let row = i * 3;
      if(board[row] >= 0 && (board[row] === board[row+1]) && (board[row] === board[row+2])) {
        return { winner: board[row], winInfo: "Row "+(row+1)};
      }
      //Check all 3 columns
      if(board[i] >= 0 && (board[i] === board[i+3]) && (board[i] === board[i+6])) {
        return { winner: board[i], winInfo: "Column "+(i+1)};
      }
    }
    //Check right dir diagnol
    if(board[0] >= 0 && (board[0] === board[4]) && (board[0] === board[8])) {
      return { winner: board[0], winInfo: "L to R Diagnol"};
    }
    //Check left dir diagnol
    if(board[2] >= 0 && (board[2] === board[4]) && (board[2] === board[6])) {
      return { winner: board[2], winInfo: "R to L Diagnol"};
    }

    return false;
  }
})
