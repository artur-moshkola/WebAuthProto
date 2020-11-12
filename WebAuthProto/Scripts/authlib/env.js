let confApiPath = '/fido2api';

let confOnError = function (msg, err) {
    alert(msg + err.toString());
}

export const config = {
    set onError(handler) {
        confOnError = handler;
    },
    get onError() {
        return confOnError;
    },
    set apiPath(path) {
        confApiPath = path;
    },
    get apiPath() {
        return confApiPath;
    }
};

export function makeApiPath(action) {
    return confApiPath + '/' + action;
}

export function onError(msg, err) {
    confOnError(msg, err);
}