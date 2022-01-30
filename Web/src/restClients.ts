import { RestClientBase } from './restClientBase';

export class AuthClient extends RestClientBase {
    private http: { fetch(url: RequestInfo, init?: RequestInit): Promise<Response> };
    private baseUrl: string;
    protected jsonParseReviver: ((key: string, value: any) => any) | undefined = undefined;

    constructor(baseUrl?: string, http?: { fetch(url: RequestInfo, init?: RequestInit): Promise<Response> }) {
        super();
        this.http = http ? http : window;
        this.baseUrl = this.getBaseUrl("", baseUrl);
    }

    login(login: Login): Promise<AuthenticatedUser | null> {
        let url_ = this.baseUrl + "/authenticate";
        url_ = url_.replace(/[?&]$/, "");

        const content_ = JSON.stringify(login);

        let options_ = {
            body: content_,
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "Accept": "application/json"
            }
        };

        return this.transformOptions(options_).then(transformedOptions_ => {
            return this.http.fetch(url_, transformedOptions_);
        }).then((_response: Response) => {
            return this.transformResult(url_, _response, (_response: Response) => this.processLogin(_response));
        });
    }

    protected processLogin(response: Response): Promise<AuthenticatedUser | null> {
        const status = response.status;
        let _headers: any = {}; if (response.headers && response.headers.forEach) { response.headers.forEach((v: any, k: any) => _headers[k] = v); };
        if (status === 200) {
            return response.text().then((_responseText) => {
                let result200: any = null;
                result200 = _responseText === "" ? null : JSON.parse(_responseText, this.jsonParseReviver) as AuthenticatedUser;
                return result200;
            });
        } else if (status !== 200 && status !== 204) {
            return response.text().then((_responseText) => {
                return throwException("An unexpected server error occurred.", status, _responseText, _headers);
            });
        }
        return Promise.resolve<AuthenticatedUser | null>(null);
    }
}

export class ChallengeClient extends RestClientBase {
    private http: { fetch(url: RequestInfo, init?: RequestInit): Promise<Response> };
    private baseUrl: string;
    protected jsonParseReviver: ((key: string, value: any) => any) | undefined = undefined;

    constructor(baseUrl?: string, http?: { fetch(url: RequestInfo, init?: RequestInit): Promise<Response> }) {
        super();
        this.http = http ? http : window;
        this.baseUrl = this.getBaseUrl("", baseUrl);
    }

    getChallenges(): Promise<AllChallenges | null> {
        let url_ = this.baseUrl + "/challenges";
        url_ = url_.replace(/[?&]$/, "");

        let options_ = {
            method: "GET",
            headers: {
                "Accept": "application/json"
            }
        };

        return this.transformOptions(options_).then(transformedOptions_ => {
            return this.http.fetch(url_, transformedOptions_);
        }).then((_response: Response) => {
            return this.transformResult(url_, _response, (_response: Response) => this.processGetChallenges(_response));
        });
    }

    protected processGetChallenges(response: Response): Promise<AllChallenges | null> {
        const status = response.status;
        let _headers: any = {}; if (response.headers && response.headers.forEach) { response.headers.forEach((v: any, k: any) => _headers[k] = v); };
        if (status === 200) {
            return response.text().then((_responseText) => {
                let result200: any = null;
                result200 = _responseText === "" ? null : JSON.parse(_responseText, this.jsonParseReviver) as AllChallenges;
                return result200;
            });
        } else if (status !== 200 && status !== 204) {
            return response.text().then((_responseText) => {
                return throwException("An unexpected server error occurred.", status, _responseText, _headers);
            });
        }
        return Promise.resolve<AllChallenges | null>(null);
    }

    joinChallenge(challengeId: string): Promise<void> {
        let url_ = this.baseUrl + "/challenges/" + challengeId + "/join";
        url_ = url_.replace(/[?&]$/, "");

        let options_ = {
            method: "POST",
            headers: {
                "Accept": "application/json"
            }
        };

        return this.transformOptions(options_).then(transformedOptions_ => {
            return this.http.fetch(url_, transformedOptions_);
        }).then((_response: Response) => {
            return this.transformResult(url_, _response, (_response: Response) => this.processJoinOrLeaveChallenge(_response));
        });
    }

    leaveChallenge(challengeId: string): Promise<void> {
        let url_ = this.baseUrl + "/challenges/" + challengeId + "/leave";
        url_ = url_.replace(/[?&]$/, "");

        let options_ = {
            method: "POST",
            headers: {
                "Accept": "application/json"
            }
        };

        return this.transformOptions(options_).then(transformedOptions_ => {
            return this.http.fetch(url_, transformedOptions_);
        }).then((_response: Response) => {
            return this.transformResult(url_, _response, (_response: Response) => this.processJoinOrLeaveChallenge(_response));
        });
    }

    protected processJoinOrLeaveChallenge(response: Response): Promise<void> {
        const status = response.status;
        let _headers: any = {}; if (response.headers && response.headers.forEach) { response.headers.forEach((v: any, k: any) => _headers[k] = v); };
        if (status !== 200 && status !== 204) {
            return response.text().then((_responseText) => {
                return throwException("An unexpected server error occurred.", status, _responseText, _headers);
            });
        }
        return Promise.resolve();
    }

    createChallenge(challenge: ChallengeFromUserDto): Promise<void> {
        let url_ = this.baseUrl + "/challenges";
        url_ = url_.replace(/[?&]$/, "");

        const content_ = JSON.stringify(challenge);

        let options_ = {
            method: "POST",
            body: content_,
            headers: {
                "Accept": "application/json"
            }
        };

        return this.transformOptions(options_).then(transformedOptions_ => {
            return this.http.fetch(url_, transformedOptions_);
        }).then((_response: Response) => {
            return this.transformResult(url_, _response, (_response: Response) => this.processCreateChallenge(_response));
        });
    }

    protected processCreateChallenge(response: Response): Promise<void> {
        const status = response.status;
        let _headers: any = {}; if (response.headers && response.headers.forEach) { response.headers.forEach((v: any, k: any) => _headers[k] = v); };
        if (status !== 200 && status !== 201 && status !== 204) {
            return response.text().then((_responseText) => {
                return throwException("An unexpected server error occurred.", status, _responseText, _headers);
            });
        }
        return Promise.resolve();
    }

    editChallenge(challengeId: string, challenge: ChallengeFromUserDto): Promise<void> {
        let url_ = this.baseUrl + "/challenges/" + challengeId;
        url_ = url_.replace(/[?&]$/, "");

        const content_ = JSON.stringify(challenge);

        let options_ = {
            method: "PUT",
            body: content_,
            headers: {
                "Accept": "application/json"
            }
        };

        return this.transformOptions(options_).then(transformedOptions_ => {
            return this.http.fetch(url_, transformedOptions_);
        }).then((_response: Response) => {
            return this.transformResult(url_, _response, (_response: Response) => this.processEditChallenge(_response));
        });
    }

    protected processEditChallenge(response: Response): Promise<void> {
        const status = response.status;
        let _headers: any = {}; if (response.headers && response.headers.forEach) { response.headers.forEach((v: any, k: any) => _headers[k] = v); };
        if (status !== 200 && status !== 204) {
            return response.text().then((_responseText) => {
                return throwException("An unexpected server error occurred.", status, _responseText, _headers);
            });
        }
        return Promise.resolve();
    }

    deleteChallenge(challengeId: string): Promise<void> {
        let url_ = this.baseUrl + "/challenges/" + challengeId;
        url_ = url_.replace(/[?&]$/, "");

        let options_ = {
            method: "DELETE",
            headers: {
                "Accept": "application/json"
            }
        };

        return this.transformOptions(options_).then(transformedOptions_ => {
            return this.http.fetch(url_, transformedOptions_);
        }).then((_response: Response) => {
            return this.transformResult(url_, _response, (_response: Response) => this.processDeleteChallenge(_response));
        });
    }

    protected processDeleteChallenge(response: Response): Promise<void> {
        const status = response.status;
        let _headers: any = {}; if (response.headers && response.headers.forEach) { response.headers.forEach((v: any, k: any) => _headers[k] = v); };
        if (status !== 200 && status !== 204) {
            return response.text().then((_responseText) => {
                return throwException("An unexpected server error occurred.", status, _responseText, _headers);
            });
        }
        return Promise.resolve();
    }

    getChallenge(challengeId: string): Promise<ChallengeDto | null> {
        let url_ = this.baseUrl + "/challenges/" + challengeId;
        url_ = url_.replace(/[?&]$/, "");

        let options_ = {
            method: "GET",
            headers: {
                "Accept": "application/json"
            }
        };

        return this.transformOptions(options_).then(transformedOptions_ => {
            return this.http.fetch(url_, transformedOptions_);
        }).then((_response: Response) => {
            return this.transformResult(url_, _response, (_response: Response) => this.processGetChallenge(_response));
        });
    }

    protected processGetChallenge(response: Response): Promise<ChallengeDto | null> {
        const status = response.status;
        let _headers: any = {}; if (response.headers && response.headers.forEach) { response.headers.forEach((v: any, k: any) => _headers[k] = v); };
        if (status === 200) {
            return response.text().then((_responseText) => {
                let result200: any = null;
                result200 = _responseText === "" ? null : JSON.parse(_responseText, this.jsonParseReviver) as ChallengeDto;
                return result200;
            });
        } else if (status !== 200 && status !== 204) {
            return response.text().then((_responseText) => {
                return throwException("An unexpected server error occurred.", status, _responseText, _headers);
            });
        }
        return Promise.resolve<ChallengeDto | null>(null);
    }

    getAthleteChallengeInfo(challengeId: string, athleteId: string): Promise<AthleteChallengeInfo | null> {
        let url_ = this.baseUrl + "/challenges/" + challengeId + "/athletes/" + athleteId;
        url_ = url_.replace(/[?&]$/, "");

        let options_ = {
            method: "GET",
            headers: {
                "Accept": "application/json"
            }
        };

        return this.transformOptions(options_).then(transformedOptions_ => {
            return this.http.fetch(url_, transformedOptions_);
        }).then((_response: Response) => {
            return this.transformResult(url_, _response, (_response: Response) => this.processGetAthleteChallengeInfo(_response));
        });
    }

    protected processGetAthleteChallengeInfo(response: Response): Promise<AthleteChallengeInfo | null> {
        const status = response.status;
        let _headers: any = {}; if (response.headers && response.headers.forEach) { response.headers.forEach((v: any, k: any) => _headers[k] = v); };
        if (status === 200) {
            return response.text().then((_responseText) => {
                let result200: any = null;
                result200 = _responseText === "" ? null : JSON.parse(_responseText, this.jsonParseReviver) as ChallengeDto;
                return result200;
            });
        } else if (status !== 200 && status !== 204) {
            return response.text().then((_responseText) => {
                return throwException("An unexpected server error occurred.", status, _responseText, _headers);
            });
        }
        return Promise.resolve<AthleteChallengeInfo | null>(null);
    }
}

export interface AllChallenges {
    current?: ChallengeInfo[],
    upcoming?: ChallengeInfo[],
    past?: ChallengeInfo[],
}

export interface ChallengeInfo {
    id?: string,
    name?: string,
    goal?: string,
    from?: Date,
    to?: Date,
}

export interface AthleteChallengeInfo {
    athleteId?: number,
    avatarUrl?: string,
    username?: string,
    totalDistance?: number,
    activitiesCount?: number,
    activities?: ActivityInfo[],
}

export interface ActivityInfo {
    id?: number,
    name?: string,
    type?: string,
    startDateLocal?: string,
    distance?: string,
    movingTime?: string,
    kiloJoules?: string,
}

export interface ChallengeDto {
    id?: string,
    name?: string,
    totalScoreTitle?: string,
    goalType?: string,
    goalRaw?: string,
    goal?: string,
    allowedSports?: string[],
    allowedSportsIcons?: string,
    from?: Date,
    to?: Date,
    athletes: AthleteChallengeStatus[],
}

export interface AthleteChallengeStatus {
    athleteId?: number;
    username?: string;
    avatarUrl?: string;
    percentOfGoal?: number;
    totalScore?: string;
}

export interface AuthenticatedUser {
    athleteId?: string;
    accessToken?: string;
    isAdmin?: boolean;
}

export interface Login {
    code?: string | undefined;
}

export interface ChallengeFromUserDto {
    name?: string,
    goalType?: string,
    goal?: string,
    allowedSports?: string[],
    from?: Date,
    to?: Date,
}

export class ApiException extends Error {
    message: string;
    status: number;
    response: string;
    headers: { [key: string]: any; };
    result: any;

    constructor(message: string, status: number, response: string, headers: { [key: string]: any; }, result: any) {
        super();

        this.message = message;
        this.status = status;
        this.response = response;
        this.headers = headers;
        this.result = result;
    }

    protected isApiException = true;

    static isApiException(obj: any): obj is ApiException {
        return obj.isApiException === true;
    }
}

function throwException(message: string, status: number, response: string, headers: { [key: string]: any; }, result?: any): any {
    throw new ApiException(message, status, response, headers, result);
}