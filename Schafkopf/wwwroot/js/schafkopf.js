"use strict";

const modalState = {};
modalState["#reconnectModal"] = false;

try {
  setTheme(localStorage.getItem("theme"));
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
  // The following code allows a user to reconnect after reloading the page or restarting the browser
  // During development this is not useful as it is more difficult to simulate multiple users from one machine
  // append `?session=new` to the url to force a new connection
  let searchParams = new URLSearchParams(window.location.search);
  var userId = localStorage.getItem("userId");
  if (userId && !(searchParams.get("session") == "new")) {
    connection
      .invoke("ReconnectPlayer", userId, searchParams.get("game"))
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
      let searchParams = new URLSearchParams(window.location.search);
      if (!searchParams.get("game")) {
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
  var button = document.getElementById("toggleThemeButton");
  var body = document.getElementsByTagName("body")[0];
  if (theme == "Dark") {
    button.textContent = "Light";
    body.classList.add("bg-dark");
    body.classList.add("text-white");
    body.classList.remove("bg-white");
    body.classList.remove("text-dark");
  } else {
    button.textContent = "Dark";
    body.classList.add("bg-white");
    body.classList.add("text-dark");
    body.classList.remove("bg-dark");
    body.classList.remove("text-white");
  }
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

  connection.on("ReceiveHand", function (cards) {
    var hand = document.getElementById("hand");
    hand.innerHTML = "";
    for (const cardName of cards) {
      var card = document.createElement("img");
      card.src = `/carddecks/noto/${cardName}.svg`;
      card.style = "width: 12.5%;";
      card.id = cardName;
      card.addEventListener("click", function (event) {
        connection
          .invoke("PlayCard", event.srcElement.id)
          .catch(function (err) {
            return console.error(err.toString());
          });
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
  });

  connection.on("ReceiveTrick", function (cards) {
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
  .getElementById("announceSoloButton")
  .addEventListener("click", function (event) {
    connection.invoke("AnnounceGameType", "Solo").catch(function (err) {
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
  .getElementById("startButton")
  .addEventListener("click", function (event) {
    event.preventDefault();
    let searchParams = new URLSearchParams(window.location.search);
    var userName = document.getElementById("startModalUserName").value;
    if (userName === "") {
      return;
    }
    const isShortHand = localStorage.getItem("isShortHand") === 'true';
    const isBettelEnabled = localStorage.getItem("bettelEnabled") === 'true';
    const isHochzeitEnabled = localStorage.getItem("hochzeitEnabled") === 'true';
    const isKlopfenEnabled = localStorage.getItem("klopfenEnabled") === 'true';

    document.getElementById("startModalUserName").value = "";
    connection
      .invoke("AddPlayer", userName, searchParams.get("game"), isShortHand, isBettelEnabled, isHochzeitEnabled, isKlopfenEnabled)
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
    hideModal('#gameIdModal');
    let searchParams = new URLSearchParams(window.location.search);
    // TODO: Automatically set an unused gameID (number) instead of using the input field
    searchParams.set("game", document.getElementById("gameIdInput").value)
    window.location.search = searchParams.toString();

    localStorage.setItem("isShortHand", document.getElementById("kurzesBlattRadio").checked);
    localStorage.setItem("bettelEnabled", document.getElementById("bettelEnabledCheck").checked);
    localStorage.setItem("hochzeitEnabled", document.getElementById("hochzeitEnabledCheck").checked);
    localStorage.setItem("klopfenEnabled", document.getElementById("klopfenEnabledCheck").checked);
    // TODO: Invoke a CreateGame task here instead of storing these to localStorage and processing in AddPlayer
    tryReconnect();
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
      connection
        .invoke("ShowLastTrick", false).catch(function (err) {
          return console.error(err.toString());
        });
    } else if (document.getElementById("toggleLastTrickButton").textContent.trim() == "Letzten Stich zeigen") {
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
  .getElementById("toggleThemeButton")
  .addEventListener("click", function (event) {
    var button = document.getElementById("toggleThemeButton");
    setTheme(button.textContent.trim());
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