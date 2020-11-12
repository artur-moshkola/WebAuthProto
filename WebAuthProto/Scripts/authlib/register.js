import { makeApiPath, onError } from './env.js';
import { coerceToArray, coerceToBase64Url } from './tools.js';

export async function register() {
    let makeCredentialOptions;
    try {
        makeCredentialOptions = await fetchMakeCredentialOptions();
    } catch (e) {
        console.error(e);
        let msg = "Exception during fetchMakeCredentialOptions";
        onError(msg, e);
    }

    console.log("Credential Options Object", makeCredentialOptions);

    if (makeCredentialOptions.status !== "ok") {
        console.log("Error creating credential options");
        console.log(makeCredentialOptions.errorMessage);
        onError(makeCredentialOptions.errorMessage);
        return;
    }

    // Turn the challenge back into the accepted format of padded base64
    makeCredentialOptions.challenge = coerceToArray(makeCredentialOptions.challenge);
    // Turn ID into a UInt8Array Buffer for some reason
    makeCredentialOptions.user.id = coerceToArray(makeCredentialOptions.user.id);

    makeCredentialOptions.excludeCredentials = makeCredentialOptions.excludeCredentials.map((c) => {
        c.id = coerceToArray(c.id);
        return c;
    });

    if (makeCredentialOptions.authenticatorSelection.authenticatorAttachment === null) delete makeCredentialOptions.authenticatorSelection.authenticatorAttachment;

    if (!makeCredentialOptions.excludeCredentials || makeCredentialOptions.excludeCredentials.length == 0) delete makeCredentialOptions.excludeCredentials;

    delete makeCredentialOptions.status;
    delete makeCredentialOptions.errorMessage;

    console.log("Credential Options Formatted", makeCredentialOptions);

    

    console.log("Creating PublicKeyCredential...");

    let newCredential;
    try {
        newCredential = await navigator.credentials.create({
            publicKey: makeCredentialOptions
        });
    } catch (e) {
        var msg = "Could not create credentials in browser"
        console.error(msg, e);
        onError(msg, e);
        return;
    }


    console.log("PublicKeyCredential Created", newCredential);

    try {
        registerNewCredential(newCredential);
    } catch (e) {
        let msg = "Exception during registerNewCredential";
        onError(msg, e);
    }
}

async function fetchMakeCredentialOptions() {
    let response = await fetch(makeApiPath('makeCredentialOptions'), {
        method: 'GET',
        headers: {
            'Accept': 'application/json'
        }
    });

    let data = await response.json();

    return data;
}


// This should be used to verify the auth data with the server
async function registerNewCredential(newCredential) {
    // Move data into Arrays incase it is super long
    let attestationObject = new Uint8Array(newCredential.response.attestationObject);
    let clientDataJSON = new Uint8Array(newCredential.response.clientDataJSON);
    let rawId = new Uint8Array(newCredential.rawId);

    const data = {
        id: newCredential.id,
        rawId: coerceToBase64Url(rawId),
        type: newCredential.type,
        extensions: newCredential.getClientExtensionResults(),
        response: {
            AttestationObject: coerceToBase64Url(attestationObject),
            clientDataJson: coerceToBase64Url(clientDataJSON)
        }
    };

    let response;
    try {
        response = await registerCredentialWithServer(data);
    } catch (e) {
        let msg = "Exception during registerCredentialWithServer";
        onError(msg, e);
        return;
    }

    console.log("Credential Object", response);

    // show error
    if (response.status !== "ok") {
        console.log("Error creating credential");
        console.log(response.errorMessage);
        onError(response.errorMessage);
        return;
    }

    
    // redirect to dashboard?
    //window.location.href = "/dashboard/" + state.user.displayName;
}

async function registerCredentialWithServer(formData) {
    let response = await fetch(makeApiPath('makeCredential'), {
        method: 'POST', // or 'PUT'
        body: JSON.stringify(formData), // data can be `string` or {object}!
        headers: {
            'Accept': 'application/json',
            'Content-Type': 'application/json'
        }
    });

    let data = await response.json();

    return data;
}
