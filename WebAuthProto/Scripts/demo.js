import { config } from './authlib/env.js';
import { register } from './authlib/register.js';
import { login } from './authlib/login.js';
import { detectFIDOSupport } from './authlib/tools.js';

document.addEventListener("DOMContentLoaded", async function () {

    config.onError = function (msg, err) {
        var node = document.createElement("p");
        var msg = '<b style="color: red;">' + msg + '</b>';
        if (err) msg = msg + '<i>' + err.toString() + '</i>';
        node.innerHTML = msg;
        document.getElementById('messages').appendChild(node);
    }

    async function updateUserInfo() {
        var auth = await getUser();

        if (auth.isAuth) {
            document.getElementById('curUser').innerText = auth.user.name;
        } else {
            document.getElementById('curUser').innerText = "";
        }
    }

    updateUserInfo();

    document.getElementById('fake').addEventListener('click', handleFakeAuth);

    async function handleFakeAuth(event) {
        event.preventDefault();
        debugger;
        let username = document.getElementById('username').value;
        await authUser(username);
        await updateUserInfo();
    }

    document.getElementById('register').addEventListener('click', handleRegister);

    async function handleRegister(event) {
        event.preventDefault();
        debugger;
        await register();
    }

    document.getElementById('login').addEventListener('click', handleLogin);

    async function handleLogin(event) {
        event.preventDefault();
        debugger;
        await login();
        await updateUserInfo();
    }

    document.getElementById('logout').addEventListener('click', handleLogout);

    async function handleLogout(event) {
        event.preventDefault();
        debugger;
        await logout();
        await updateUserInfo();
    }

    document.getElementById('test').addEventListener('click', handleTest);

    function handleTest(event) {
        event.preventDefault();
        if (detectFIDOSupport()) alert("supported");
        else alert("NOT supported");
    }

});


async function getUser() {
    let response = await fetch('/demoapi/getCurrentUser', {
        method: 'GET', // or 'PUT'
        headers: {
            'Accept': 'application/json'
        }
    });

    let data = await response.json();

    return data;
}

async function authUser(username) {
    var formData = new FormData();
    formData.append('username', username);

    let response = await fetch('/demoapi/fakeAuthenticate', {
        method: 'POST', // or 'PUT'
        body: formData, // data can be `string` or {object}!
        headers: {
            'Accept': 'application/json'
        }
    });

    let data = await response.json();

    return data.status == "ok";
}

async function logout() {
    let response = await fetch('/demoapi/logOut', {
        method: 'GET', // or 'PUT'
        headers: {
            'Accept': 'application/json'
        }
    });

    let data = await response.json();

    return data.status == "ok";
}