"use strict";

const modalState = {};
modalState["#reconnectModal"] = false;

const stylesheet = document.styleSheets[1];
const cardOnTableCSSItemStyle = [...stylesheet.cssRules].find((r) => r.selectorText === ".card-on-table").style;
// Variables to store z-indices (-> CSS) of cards on table
var zIndexCardsCurrentTrick = [-5, -5, -5, -5];
var zIndexCardsLastTrick = [-5, -5, -5, -5];

try {
  setTheme(localStorage.getItem("theme"));
} catch { }

try {
  setCardsOnTableTheme(localStorage.getItem("cardsOnTableTheme"), true);
} catch { }

let connection;
connect();

function showModal(modal) {
  modalState[modal] = true;
  $(modal).modal({ keyboard: false, backdrop: "static" });

  $(modal).on('hidden.bs.modal', function (e) {
    if (modalState[modal]) {
      $(modal).modal({ keyboard: false, backdrop: "static" });
    }
  })
}

function hideModal(modal) {
  modalState[modal] = false;
  $(modal).modal('hide');

  $(modal).on('shown.bs.modal', function (e) {
    if (!modalState[modal]) {
      $(modal).modal('hide');
    }
  })
}

function tryReconnect() {
  var userId = localStorage.getItem("userId");
  var gameId = localStorage.getItem("gameId");
  if (userId && gameId) {
    connection
      .invoke("ReconnectPlayer", userId, gameId)
      .catch(function (err) {
        return console.error(err.toString());
      });
  } else {
    document.getElementById("startModalUserName").value = localStorage.getItem("userName");
    showModal('#usernameModal');
    $('#usernameModal').on('shown.bs.modal', function () {
      $('#startModalUserName').focus();
    })
  }
}

function connect() {
  connection = new signalR.HubConnectionBuilder()
    .withUrl("/schafkopfHub")
    .build();
  connection
    .start()
    .then(function () {
      init();
      hideModal("#reconnectModal");
      document.getElementById("sendButton").disabled = false;
      if (!localStorage.getItem("gameId") || !localStorage.getItem("userId")) {
        // Clear any existing localStorage data for a fresh start
        localStorage.removeItem("gameId");
        localStorage.removeItem("userId");
        showModal('#gameIdModal');
        $('#gameIdModal').on('shown.bs.modal', function () {
          $('#gameIdInput').focus();
        })
        return;
      }
      tryReconnect();
    })
    .catch(function (err) {
      showModal("#reconnectModal");
      return console.error(err.toString());
    });
}

function setTheme(theme) {
  localStorage.setItem("theme", theme);
  // var button = document.getElementById("toggleThemeButton");
  var body = document.getElementsByTagName("body")[0];
  if (theme == "Dark") {
    // button.textContent = "Light";
    body.classList.add("bg-dark");
    body.classList.add("text-white");
    body.classList.remove("bg-white");
    body.classList.remove("text-dark");
  } else {
    // button.textContent = "Dark";
    body.classList.add("bg-white");
    body.classList.add("text-dark");
    body.classList.remove("bg-dark");
    body.classList.remove("text-white");
  }
}

function setCardsOnTableTheme(theme, forceSetting) {
  var currentTheme = "none";
  try {
    currentTheme = localStorage.getItem("cardsOnTableTheme");
  } catch { }

  if ((theme != currentTheme) || forceSetting) {
    localStorage.setItem("cardsOnTableTheme", theme);
    if (theme == "2dStraight") {
      cardOnTableCSSItemStyle.setProperty('--rotateCardBottom', '0deg');
      cardOnTableCSSItemStyle.setProperty('--translateCardBottom', '27.5%');
      cardOnTableCSSItemStyle.setProperty('--rotateCardLeft', '90deg');
      cardOnTableCSSItemStyle.setProperty('--translateCardLeft', '27.5%');
      cardOnTableCSSItemStyle.setProperty('--rotateCardTop', '180deg');
      cardOnTableCSSItemStyle.setProperty('--translateCardTop', '27.5%');
      cardOnTableCSSItemStyle.setProperty('--rotateCardRight', '270deg');
      cardOnTableCSSItemStyle.setProperty('--translateCardRight', '27.5%');
    } else {
      cardOnTableCSSItemStyle.setProperty('--rotateCardBottom', getRandomInt(-15, 15) + 'deg');
      cardOnTableCSSItemStyle.setProperty('--translateCardBottom', getRandomInt(15, 225) / 10 + '%');
      cardOnTableCSSItemStyle.setProperty('--rotateCardLeft', getRandomInt(75, 105) + 'deg');
      cardOnTableCSSItemStyle.setProperty('--translateCardLeft', getRandomInt(15, 225) / 10 + '%');
      cardOnTableCSSItemStyle.setProperty('--rotateCardTop', getRandomInt(165, 195) + 'deg');
      cardOnTableCSSItemStyle.setProperty('--translateCardTop', getRandomInt(15, 225) / 10 + '%');
      cardOnTableCSSItemStyle.setProperty('--rotateCardRight', getRandomInt(255, 285) + 'deg');
      cardOnTableCSSItemStyle.setProperty('--translateCardRight', getRandomInt(15, 225) / 10 + '%');
    }
  }
}

function getRandomInt(min, max) {
  min = Math.ceil(min);
  max = Math.floor(max);
  return Math.floor(Math.random() * (max - min) + min);
}

function getZIndexOfCardsOnTable() {
  return [
    parseInt(cardOnTableCSSItemStyle.getPropertyValue('--zIndexCardBottom')),
    parseInt(cardOnTableCSSItemStyle.getPropertyValue('--zIndexCardLeft')),
    parseInt(cardOnTableCSSItemStyle.getPropertyValue('--zIndexCardTop')),
    parseInt(cardOnTableCSSItemStyle.getPropertyValue('--zIndexCardRight'))
  ];
}

function setZIndexOfCardsOnTable(z_bottom, z_left, z_top, z_right) {
  cardOnTableCSSItemStyle.setProperty('--zIndexCardBottom',z_bottom);
  cardOnTableCSSItemStyle.setProperty('--zIndexCardLeft',z_left);
  cardOnTableCSSItemStyle.setProperty('--zIndexCardTop',z_top);
  cardOnTableCSSItemStyle.setProperty('--zIndexCardRight',z_right);
}

function init() {

  connection.onclose(() => {
    document.getElementById("game-info").textContent = "";
    document.getElementById("hand").innerHTML = "";
    document.getElementById("card-bottom").src = "/carddecks/blank.svg";
    document.getElementById("card-left").src = "/carddecks/blank.svg";
    document.getElementById("card-top").src = "/carddecks/blank.svg";
    document.getElementById("card-right").src = "/carddecks/blank.svg";
    const btn = document.getElementById("take-trick-btn");
    btn.classList.add("d-none");
    btn.classList.remove("btn-primary");
    btn.classList.remove("btn-secondary");
    document.getElementById("take-trick-btn-content").textContent = "";
    hideModal('#gameOverModal');
    hideModal('#gameIdModal');
    hideModal('#announceModal');
    hideModal('#knockModal');
    hideModal('#announceGameTypeModal');
    hideModal('#gameColorModal');
    hideModal('#wantToPlayModal');
    hideModal('#wantToSpectateModal');
    hideModal('#allowSpectatorModal');
    hideModal('#usernameModal');
    if (!modalState["#reconnectModal"]) {
      const modalTitle = document.getElementById("reconnectModalTitle");
      const button = document.getElementById("reconnectButton");
      modalTitle.textContent = "Verbindung zum Server verloren!"
      button.textContent = "Erneut verbinden"
      showModal("#reconnectModal");
    }
  });

  connection.on("OfferBettel", function (isBettelEnabled) {
    var x = document.getElementById("announceBettelButton");
    if (isBettelEnabled === "True") {
      x.style.display = "block";
    } else {
      x.style.display = "none";
    }
  });

  connection.on("ReceiveChatMessage", function (user, message) {
    var div = document.createElement("div");
    var userB = div.appendChild(document.createElement("b"));
    var msgSpan = div.appendChild(document.createElement("span"));
    userB.textContent = `${user}: `;
    msgSpan.textContent = message;
    document.getElementById("messagesList").appendChild(div);
    var messageList = document.getElementById("messagesList");
    messageList.scrollTop = messageList.scrollHeight;
  });

  connection.on("ReceiveSystemMessage", function (message) {
    if (message.startsWith("Error: ")) {
      document.getElementById("errorModalBody").textContent = message;
      $("#errorModal").modal();
    }

    var div = document.createElement("div");
    var userB = div.appendChild(document.createElement("b"));
    var msgSpan = div.appendChild(document.createElement("span"));
    userB.textContent = "System: ";
    msgSpan.textContent = message;
    document.getElementById("messagesList").appendChild(div);
    var messageList = document.getElementById("messagesList");
    messageList.scrollTop = messageList.scrollHeight;
  });

  connection.on("ReceiveError", function (message) {
    document.getElementById("errorModalBody").textContent = message;
    $("#errorModal").modal();
    
    // If we get an error and have a pre-selected card, unselect it since it's invalid
    if (preSelectedCard) {
      const card = document.getElementById(preSelectedCard);
      if (card) {
        card.style.border = "";
        card.style.borderRadius = "";
        card.style.boxShadow = "";
      }
      preSelectedCard = null;
    }
  });

  connection.on("ReceiveInfo", function (message) {
    document.getElementById("infoModalBody").textContent = message;
    $("#infoModal").modal();
  });

  connection.on("AskUsername", function (message) {
    localStorage.removeItem("userId");
    document.getElementById("startModalUserName").value = localStorage.getItem("userName");
    showModal('#usernameModal');
    $('#usernameModal').on('shown.bs.modal', function () {
      $('#startModalUserName').focus();
    })
  });

  connection.on("GameOver", function (title, body) {
    // Clear any revealed cards when game is over
    const existingRevealedHands = document.querySelectorAll('.revealed-hand');
    existingRevealedHands.forEach(hand => hand.remove());
    
    document.getElementById("gameOverModalTitle").textContent = title;
    document.getElementById("gameOverModalBody").textContent = body;
    showModal('#gameOverModal');
  });

  connection.on("AskAnnounce", function (message) {
    document.getElementById("announceModalTitle").textContent = "Magst du spielen?";
    showModal('#announceModal');
  });

  connection.on("AskGameType", function (message) {
    showModal('#announceGameTypeModal');
  });

  connection.on("AskColor", function (message) {
    showModal('#gameColorModal');
  });

  connection.on("OpenWantToKnockModal", function (message) {
    showModal('#knockModal');
  });

  connection.on("AskWantToPlay", function (players, startPlayer, proposal) {
    document.getElementById("wantToPlayModalBody").innerHTML = "";
    const playersDiv = document.createElement("div");
    playersDiv.textContent = `Spieler: ${players}`
    const startPlayerDiv = document.createElement("div");
    startPlayerDiv.textContent = `${startPlayer} kommt als nächstes raus.`
    const proposalDiv = document.createElement("div");
    proposalDiv.textContent = `Vorschlag: ${proposal}`
    document.getElementById("wantToPlayModalBody").appendChild(playersDiv);
    document.getElementById("wantToPlayModalBody").appendChild(startPlayerDiv);
    document.getElementById("wantToPlayModalBody").appendChild(proposalDiv);
    showModal('#wantToPlayModal');
  });

  connection.on("AskAnnounceHochzeit", function (message) {
    document.getElementById("announceModalTitle").textContent = "Magst du eine Hochzeit anbieten?";
    showModal('#announceModal');
  });

  connection.on("AskWantToMarryPlayer", function (message) {
    document.getElementById("announceModalTitle").textContent = `Magst du ${message} heiraten?`;
    showModal('#announceModal');
  });

  connection.on("ShowContraReSupButton", function (buttonText) {
    const btn = document.getElementById("contraReSupButton");
    btn.textContent = buttonText;
    btn.disabled = false;
    btn.classList.remove("d-none");
  });

  connection.on("HideContraReSupButton", function () {
    document.getElementById("contraReSupButton").classList.add("d-none");
  });

  connection.on("AskWantToSpectate", function (players) {
    document.getElementById("wantToSpectatePlayer1Button").textContent = players[0];
    document.getElementById("wantToSpectatePlayer2Button").textContent = players[1];
    document.getElementById("wantToSpectatePlayer3Button").textContent = players[2];
    document.getElementById("wantToSpectatePlayer4Button").textContent = players[3];
    showModal('#wantToSpectateModal');
  });

  connection.on("AskAllowSpectator", function (player) {
    document.getElementById("allowSpectatorModalTitle").textContent = `Erlaubst du ${player} bei dir zuzuschauen?`;
    showModal('#allowSpectatorModal');
  })

  connection.on("CloseGameOverModal", function () {
    hideModal('#gameOverModal');
  });

  connection.on("CloseAnnounceModal", function () {
    hideModal('#announceModal');
  });

  connection.on("CloseKnockModal", function () {
    hideModal('#knockModal');
  });

  connection.on("CloseAnnounceGameTypeModal", function () {
    hideModal('#announceGameTypeModal');
  });

  connection.on("CloseGameColorModal", function () {
    hideModal('#gameColorModal');
  });

  connection.on("CloseWantToPlayModal", function () {
    hideModal('#wantToPlayModal');
  });

  connection.on("CloseWantToSpectateModal", function () {
    hideModal('#wantToSpectateModal');
  });

  connection.on("CloseAllowSpectatorModal", function (player) {
    hideModal('#allowSpectatorModal');
  })

  connection.on("StoreUser", function (id, name) {
    localStorage.setItem("userId", id);
    localStorage.setItem("userName", name);
    hideModal('#usernameModal');
  });

  // Track pre-selected card
  let preSelectedCard = null;

  connection.on("RevealPlayerCards", function(playerName, cards) {
    // Remove any existing revealed cards first
    const existingRevealedHands = document.querySelectorAll('.revealed-hand');
    existingRevealedHands.forEach(hand => hand.remove());

    // Try each possible position (top, left, right) to find the player
    const positions = ['top', 'left', 'right'];
    for (const pos of positions) {
      const playerNameDiv = document.getElementById(`player-${pos}-name`);
      if (playerNameDiv && playerNameDiv.textContent.includes(playerName)) {
        const hand = document.createElement("div");
        hand.className = 'revealed-hand';
        
        // Different styling based on position
        if (pos === 'top') {
          hand.style = "position: absolute; top: 60px; left: 50%; transform: translateX(-50%); text-align: center; z-index: 1000;";
        } else if (pos === 'left') {
          hand.style = "position: absolute; left: 60px; top: 50%; transform: translateY(-50%); text-align: center; z-index: 1000;";
        } else if (pos === 'right') {
          hand.style = "position: absolute; right: 60px; top: 50%; transform: translateY(-50%); text-align: center; z-index: 1000;";
        }

        // Create a label
        const label = document.createElement("div");
        label.textContent = "Bettel Brett";
        label.style = "color: orange; font-weight: bold; margin-bottom: 5px;";
        hand.appendChild(label);

        // Add cards in a vertical arrangement for left/right, horizontal for top
        const cardsContainer = document.createElement("div");
        cardsContainer.style = pos === 'top' ? "display: flex; gap: 5px;" : "display: flex; flex-direction: column; gap: 5px;";
        
        for (const card of cards) {
          const cardImg = document.createElement("img");
          // Convert Color enum (number) to string representation
          const colorMap = {
            100: "Schellen",
            200: "Herz",
            300: "Gras", 
            400: "Eichel"
          };
          const colorStr = colorMap[card.color];
          cardImg.src = `/carddecks/noto/${colorStr}-${card.number}.svg`;
          // Smaller cards for left/right positions
          cardImg.style = pos === 'top' ? "width: 60px;" : "width: 60px;";
          cardsContainer.appendChild(cardImg);
        }
        hand.appendChild(cardsContainer);

        // Add to the player's container
        playerNameDiv.parentElement.appendChild(hand);
        break;
      }
    }
  });

  connection.on("ReceiveHand", function (cards) {
    var hand = document.getElementById("hand");
    hand.innerHTML = "";
    // Clear pre-selection when receiving a new hand (new trick)
    preSelectedCard = null;
    
    for (const cardName of cards) {
      var card = document.createElement("img");
      card.src = `/carddecks/noto/${cardName}.svg`;
      card.style = "width: 12.5%;";
      card.id = cardName;
      
      // Add pre-selection style if this card was pre-selected
      if (cardName === preSelectedCard) {
        card.style.border = "3px solid orange";
        card.style.borderRadius = "5px";
        card.style.boxShadow = "0 0 5px rgba(255, 165, 0, 0.5)";
      }

      card.addEventListener("click", function (event) {
        const cardElement = event.currentTarget;
        const clickedCard = cardElement.id;
        const isMyTurn = document.getElementById("player-bottom-name").classList.contains("active-player");
        
        // If it's not my turn, handle pre-selection
        if (!isMyTurn) {
          // Toggle pre-selection
          if (preSelectedCard === clickedCard) {
            // Unselect
            preSelectedCard = null;
            cardElement.style.border = "";
            cardElement.style.borderRadius = "";
            cardElement.style.boxShadow = "";
          } else {
            // Clear previous pre-selection if any
            if (preSelectedCard) {
              const prevCard = document.getElementById(preSelectedCard);
              if (prevCard) {
                prevCard.style.border = "";
                prevCard.style.borderRadius = "";
                prevCard.style.boxShadow = "";
              }
            }
            // Set new pre-selection
            preSelectedCard = clickedCard;
            cardElement.style.border = "3px solid orange";
            cardElement.style.borderRadius = "5px";
            cardElement.style.boxShadow = "0 0 5px rgba(255, 165, 0, 0.5)";
          }
        } else {
          // If it's my turn, play the card
          connection
            .invoke("PlayCard", clickedCard)
            .catch(function (err) {
              return console.error(err.toString());
            });
          // Clear pre-selection after playing
          preSelectedCard = null;
        }
        event.preventDefault();
      });
      hand.appendChild(card);
    }
  });

  connection.on("ReceivePlayers", function (players, infos, actionPlayer) {
    const playersPositions = new Array("player-bottom", "player-left", "player-top", "player-right");
    for (let i = 0; i < 4; i++) {
      const player = document.getElementById(playersPositions[i] + "-name");
      const info = document.getElementById(playersPositions[i] + "-info");
      player.textContent = players[i];
      info.textContent = infos[i]
      if (i == actionPlayer) {
        player.classList.add("active-player");
        // Auto-play pre-selected card if it's our turn and we have one selected
        if (preSelectedCard) {
          connection
            .invoke("PlayCard", preSelectedCard)
            .then(() => {
              // Only clear pre-selection if the play was successful
              preSelectedCard = null;
            })
            .catch(function (err) {
              // Error is handled by ReceiveError event handler
              // Do not clear preSelectedCard here as it will be cleared by ReceiveError if needed
              return console.error(err.toString());
            });
        }
      } else {
        player.classList.remove("active-player");
      }
    }
  });

  connection.on("ReceiveGameInfo", function (message) {
    const info = document.getElementById("game-info");
    if (info.textContent == "" && message != "") {
      info.textContent = message;
    } else {
      info.textContent += "\r\n" + message;
    }
    info.scrollTop = info.scrollHeight;
  });

  connection.on("ReceiveTrick", function (cards) {
    zIndexCardsCurrentTrick = getZIndexOfCardsOnTable();
    // Update z-indices (-> CSS) of cards on table if <4 cards are on table: increment based on z-index of predessor card (if existent)
    if (!(cards[0] != "" && cards[1] != "" && cards[2] != "" && cards[3] != "")) {
      var zIndexCardBottom = cards[3] != "" ? zIndexCardsCurrentTrick[3]+1 : -5;
      var zIndexCardLeft= cards[0] != "" ? zIndexCardsCurrentTrick[0]+1 : -5;
      var zIndexCardTop = cards[1] != "" ? zIndexCardsCurrentTrick[1]+1 : -5;
      var zIndexCardRight = cards[2] != "" ? zIndexCardsCurrentTrick[2]+1 : -5;
      setZIndexOfCardsOnTable(zIndexCardBottom, zIndexCardLeft, zIndexCardTop, zIndexCardRight);
      zIndexCardsCurrentTrick = [zIndexCardBottom, zIndexCardLeft, zIndexCardTop, zIndexCardRight];
    }
    // Store z-indices for "see last trick button" and clear preselection when trick is complete
    else
    {
      zIndexCardsLastTrick = zIndexCardsCurrentTrick;
      // Clear preselection when all 4 cards have been played
      if (preSelectedCard) {
        const prevCard = document.getElementById(preSelectedCard);
        if (prevCard) {
          prevCard.style.border = "";
          prevCard.style.borderRadius = "";
          prevCard.style.boxShadow = "";
        }
        preSelectedCard = null;
      }
    }
    document.getElementById("card-bottom").src = cards[0] != "" ? `/carddecks/noto/${cards[0]}.svg` : "/carddecks/blank.svg";
    document.getElementById("card-left").src = cards[1] != "" ? `/carddecks/noto/${cards[1]}.svg` : "/carddecks/blank.svg";
    document.getElementById("card-top").src = cards[2] != "" ? `/carddecks/noto/${cards[2]}.svg` : "/carddecks/blank.svg";
    document.getElementById("card-right").src = cards[3] != "" ? `/carddecks/noto/${cards[3]}.svg` : "/carddecks/blank.svg";
  });

  connection.on("ReceiveLastTrickButton", function (buttonState) {
    switch (buttonState) {
      case "disabled":
        document.getElementById("toggleLastTrickButton").classList.add("d-none");
        break;
      case "show":
        document.getElementById("toggleLastTrickButton").classList.remove("d-none");
        document.getElementById("toggleLastTrickButton").textContent = "Letzten Stich zeigen";
        break;
      case "hide":
        document.getElementById("toggleLastTrickButton").classList.remove("d-none");
        document.getElementById("toggleLastTrickButton").textContent = "Letzten Stich verstecken";
        break;
    }
  });

  connection.on("ReceiveTakeTrickButton", function (buttonState, winner) {
    const btn = document.getElementById("take-trick-btn");
    const content = document.getElementById("take-trick-btn-content");
    btn.classList.remove("d-none");
    btn.classList.remove("btn-primary");
    btn.classList.remove("btn-secondary");
    switch (buttonState) {
      case "hidden":
        content.textContent = "";
        btn.classList.add("d-none");
        // Random rotation and translation for next trick
        if (localStorage.getItem("cardsOnTableTheme") == "2dRealistic") {
          cardOnTableCSSItemStyle.setProperty('--rotateCardBottom', getRandomInt(-15, 15) + 'deg');
          cardOnTableCSSItemStyle.setProperty('--translateCardBottom', getRandomInt(15, 225) / 10 + '%');
          cardOnTableCSSItemStyle.setProperty('--rotateCardLeft', getRandomInt(75, 105) + 'deg');
          cardOnTableCSSItemStyle.setProperty('--translateCardLeft', getRandomInt(15, 225) / 10 + '%');
          cardOnTableCSSItemStyle.setProperty('--rotateCardTop', getRandomInt(165, 195) + 'deg');
          cardOnTableCSSItemStyle.setProperty('--translateCardTop', getRandomInt(15, 225) / 10 + '%');
          cardOnTableCSSItemStyle.setProperty('--rotateCardRight', getRandomInt(255, 285) + 'deg');
          cardOnTableCSSItemStyle.setProperty('--translateCardRight', getRandomInt(15, 225) / 10 + '%');
        }
        break;
      case "won":
        content.textContent = "Stich nehmen!";
        btn.classList.add("btn-primary");
        break;
      case "lost":
        content.textContent = `${winner} hat den Stich gewonnen.`;
        btn.classList.add("btn-secondary");
        break;
    }
  });

  connection.on("ReceiveKicked", function (user) {
    const modalTitle = document.getElementById("reconnectModalTitle");
    const button = document.getElementById("reconnectButton");
    modalTitle.textContent = `${user} hat dich rausgeworfen!`;
    button.textContent = "Erneut beitreten"
    showModal("#reconnectModal");
    connection.stop();
  });

  connection.on("ReceivePlayersList", function (players, isPlaying) {
    const playersSpan = document.getElementById("players");
    playersSpan.innerHTML = "";
    for (let i = 0; i < players.length; i++) {
      const player = document.createElement(isPlaying[i] ? "u" : "span");
      player.textContent = players[i];
      playersSpan.appendChild(player);
      if (i < players.length - 1) {
        playersSpan.innerHTML += ", ";
      }
    }
  });
}


document
  .getElementById("sendButton")
  .addEventListener("click", function (event) {
    event.preventDefault();
    var message = document.getElementById("messageInput").value;
    if (message.trim() === "") {
      return;
    }
    connection.invoke("SendChatMessage", message).catch(function (err) {
      return console.error(err.toString());
    });
    document.getElementById("messageInput").value = "";
  });

document
  .getElementById("wantToPlayButton")
  .addEventListener("click", function (event) {
    connection.invoke("PlayerWantsToPlay").catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("wantToPauseButton")
  .addEventListener("click", function (event) {
    connection.invoke("PlayerWantsToPause").catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("announceNoButton")
  .addEventListener("click", function (event) {
    connection.invoke("Announce", false).catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("knockYesButton")
  .addEventListener("click", function (event) {
    connection.invoke("Knock", true).catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("knockNoButton")
  .addEventListener("click", function (event) {
    connection.invoke("Knock", false).catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("announceYesButton")
  .addEventListener("click", function (event) {
    connection.invoke("Announce", true).catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("announceSauspielButton")
  .addEventListener("click", function (event) {
    connection.invoke("AnnounceGameType", "Sauspiel").catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("announceGeierButton")
  .addEventListener("click", function (event) {
    connection.invoke("AnnounceGameType", "Geier").catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("announceWenzButton")
  .addEventListener("click", function (event) {
    connection.invoke("AnnounceGameType", "Wenz").catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("announceBettelButton")
  .addEventListener("click", function (event) {
    connection.invoke("AnnounceGameType", "Bettel").catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("announceBettelBrettButton")
  .addEventListener("click", function (event) {
    connection.invoke("AnnounceGameType", "BettelBrett").catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("announceSoloButton")
  .addEventListener("click", function (event) {
    connection.invoke("AnnounceGameType", "Solo").catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("announceGeierToutButton")
  .addEventListener("click", function (event) {
    connection.invoke("AnnounceGameType", "Geier-Tout").catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("announceWenzToutButton")
  .addEventListener("click", function (event) {
    connection.invoke("AnnounceGameType", "Wenz-Tout").catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("announceSoloToutButton")
  .addEventListener("click", function (event) {
    connection.invoke("AnnounceGameType", "Solo-Tout").catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("eichelButton")
  .addEventListener("click", function (event) {
    connection.invoke("AnnounceGameColor", "Eichel").catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("grasButton")
  .addEventListener("click", function (event) {
    connection.invoke("AnnounceGameColor", "Gras").catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("herzButton")
  .addEventListener("click", function (event) {
    connection.invoke("AnnounceGameColor", "Herz").catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("schellenButton")
  .addEventListener("click", function (event) {
    connection.invoke("AnnounceGameColor", "Schellen").catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("contraReSupButton")
  .addEventListener("click", function (event) {
    connection.invoke("AnnounceContraReSup").catch(function (err) {
      return console.error(err.toString());
    });
    event.preventDefault();
  });

document
  .getElementById("startButton")
  .addEventListener("click", function (event) {
    event.preventDefault();
    var userName = document.getElementById("startModalUserName").value;
    if (userName === "") {
      return;
    }

    document.getElementById("startModalUserName").value = "";
    connection
      .invoke("AddPlayer", userName, localStorage.getItem("gameId"))
      .catch(function (err) {
        return console.error(err.toString());
      });
  });

document
  .getElementById("restartButton")
  .addEventListener("click", function (event) {
    connection
      .invoke("ResetGame").catch(function (err) {
        return console.error(err.toString());
      });
    event.preventDefault();
  });

document
  .getElementById("wantToSpectatePlayer1Button")
  .addEventListener("click", function (event) {
    connection
      .invoke("PlayerWantsToSpectate", 0).catch(function (err) {
        return console.error(err.toString());
      });
    event.preventDefault();
  });

document
  .getElementById("wantToSpectatePlayer2Button")
  .addEventListener("click", function (event) {
    connection
      .invoke("PlayerWantsToSpectate", 1).catch(function (err) {
        return console.error(err.toString());
      });
    event.preventDefault();
  });

document
  .getElementById("wantToSpectatePlayer3Button")
  .addEventListener("click", function (event) {
    connection
      .invoke("PlayerWantsToSpectate", 2).catch(function (err) {
        return console.error(err.toString());
      });
    event.preventDefault();
  });

document
  .getElementById("wantToSpectatePlayer4Button")
  .addEventListener("click", function (event) {
    connection
      .invoke("PlayerWantsToSpectate", 3).catch(function (err) {
        return console.error(err.toString());
      });
    event.preventDefault();
  });

document
  .getElementById("doNotAllowSpectatorButton")
  .addEventListener("click", function (event) {
    connection
      .invoke("AllowSpectator", false).catch(function (err) {
        return console.error(err.toString());
      });
    event.preventDefault();
  });

document
  .getElementById("allowSpectatorButton")
  .addEventListener("click", function (event) {
    connection
      .invoke("AllowSpectator", true).catch(function (err) {
        return console.error(err.toString());
      });
    event.preventDefault();
  });

document
  .getElementById("gameIdSubmitButton")
  .addEventListener("click", function (event) {
    if (document.getElementById("newTableCheck").checked == true) {
      // Clear localStorage before creating a new table
      localStorage.removeItem("gameId");
      localStorage.removeItem("userId");
      connection.invoke("GameExists", document.getElementById("gameIdInput").value)
       .then((result) =>  {
        if (result == true) {
          document.getElementById("errorModalBody").textContent = "Tisch existiert bereits!";
          $("#errorModal").modal();
          return;
        } else {
          hideModal('#gameIdModal');
          connection.invoke("CreateGame",
                            document.getElementById("gameIdInput").value,
                            document.getElementById("kurzesBlattRadio").checked,
                            document.getElementById("bettelEnabledCheck").checked,
                            document.getElementById("hochzeitEnabledCheck").checked,
                            document.getElementById("klopfenEnabledCheck").checked)
                    .catch(function (err) {
                      return console.error(err.toString());
          });
          localStorage.setItem("gameId", document.getElementById("gameIdInput").value);
          tryReconnect();

       }})
       .catch(function (err) {
        return console.error(err.toString());
      });
      
    } else {
      connection.invoke("GameExists", document.getElementById("gameIdInput").value)
       .then((result) =>  {
        if (result == false) {
          document.getElementById("errorModalBody").textContent = "Tisch existiert nicht!";
          $("#errorModal").modal();
          return;
        } else {
          hideModal('#gameIdModal');
          localStorage.setItem("gameId", document.getElementById("gameIdInput").value);
          tryReconnect();
        }
      });
    }
    event.preventDefault();
  });

document
  .getElementById("reconnectButton")
  .addEventListener("click", function (event) {
    connect();
    event.preventDefault();
  });

document
  .getElementById("toggleLastTrickButton")
  .addEventListener("click", function (event) {
    if (document.getElementById("toggleLastTrickButton").textContent.trim() == "Letzten Stich verstecken") {
      setZIndexOfCardsOnTable(zIndexCardsCurrentTrick[0], zIndexCardsCurrentTrick[1], zIndexCardsCurrentTrick[2], zIndexCardsCurrentTrick[3]);
      connection
        .invoke("ShowLastTrick", false).catch(function (err) {
          return console.error(err.toString());
        });
    } else if (document.getElementById("toggleLastTrickButton").textContent.trim() == "Letzten Stich zeigen") {
      setZIndexOfCardsOnTable(zIndexCardsLastTrick[0], zIndexCardsLastTrick[1], zIndexCardsLastTrick[2], zIndexCardsLastTrick[3]);
      connection
        .invoke("ShowLastTrick", true).catch(function (err) {
          return console.error(err.toString());
        });
    }
    event.preventDefault();
  });

document
  .getElementById("take-trick-btn")
  .addEventListener("click", function (event) {
    connection
      .invoke("TakeTrick").catch(function (err) {
        return console.error(err.toString());
      });
    event.preventDefault();
  });

document
  .getElementById("toggleUserSettingsButton")
  .addEventListener("click", function (event) {
    // Update Radio Buttons
    if (localStorage.getItem("theme") == "Dark") {
      document.getElementById("themeOptionLight").checked = false;
      document.getElementById("themeOptionDark").checked = true;
    } else {
      document.getElementById("themeOptionDark").checked = false;
      document.getElementById("themeOptionLight").checked = true;
    }
    if (localStorage.getItem("cardsOnTableTheme") == "2dStraight") {
      document.getElementById("cardsOnTableOption2dRealistic").checked = false;
      document.getElementById("cardsOnTableOptionStraight").checked = true;
    } else {
      document.getElementById("cardsOnTableOptionStraight").checked = false;
      document.getElementById("cardsOnTableOption2dRealistic").checked = true;
    }

    showModal('#userSettingsModal');
    event.preventDefault();
  });

document
  .getElementById("userSettingsSubmitButton")
  .addEventListener("click", function (event) {
    hideModal('#userSettingsModal');
    if (document.getElementById("themeOptionDark").checked) {
      setTheme("Dark");
    } else {
      setTheme("Light");
    }
    if (document.getElementById("cardsOnTableOptionStraight").checked) {
      setCardsOnTableTheme("2dStraight", false);
    } else {
      setCardsOnTableTheme("2dRealistic", false);
    }
    event.preventDefault();
  });

document
  .getElementById("player-bottom-name")
  .addEventListener("click", function (event) {
    document.getElementById("startModalUserName").value = localStorage.getItem("userName");
    showModal('#usernameModal');
    $('#usernameModal').on('shown.bs.modal', function () {
      $('#startModalUserName').focus();
    })
    event.preventDefault();
  });

  function showNewTableOptions(checkbox) {
    // TODO: Take precautions that no duplicate games are created
    var ckName = document.getElementsByName("newTableOption");
    var checked = document.getElementById(checkbox.id);

    if (checked.checked) {
      for (var i=0; i < ckName.length; i++) {
        ckName[i].disabled = false;
      } 
    } else {
      for (var i=0; i < ckName.length; i++) {
        ckName[i].disabled = true;
      } 
    }    
}