import { API_URL } from './helpers';
import { LogoutListeners } from './AuthContext';

export class RestClientBase {

    private getDefaultHeaders = (): Record<string, string> => {
        const defaultHeader: Record<string, string> = {
            'Content-Type': 'application/json',
            'X-Custom-Authorization': `Bearer ${localStorage.getItem('accessToken')}`,
        };
        return defaultHeader;
    }

    protected getBaseUrl = (defaultUrl: string, passedBaseUrl: string): string => {
        if (passedBaseUrl) {
            return passedBaseUrl;
        }
        if (defaultUrl) {
            return defaultUrl;
        }
        return API_URL;
    }

    protected transformOptions(options: RequestInit): Promise<RequestInit> {
        const defaultHeaders = this.getDefaultHeaders();
        const headersDict = new Map<string, string>();
        const keys = Object.getOwnPropertyNames(defaultHeaders);
        keys.forEach((key) => {
            const value: string = defaultHeaders[key];
            headersDict.set(key, value);
        });
        const keys2 = Object.getOwnPropertyNames(options.headers);
        keys2.forEach((key) => {
            const value: string = options.headers[key];
            headersDict.set(key, value);
        });
        const headers: Record<string, string> = {};
        headersDict.forEach((value, key) => {
            headers[key] = value;
        });
        const updatedOptions: RequestInit = {
            headers,
            body: options.body,
            method: options.method,
        };
        return Promise.resolve(updatedOptions);
    }

    protected transformResult = (url: string, response: Response, processor: (response: Response) => any) => {
        if (response.status === 401) {
            LogoutListeners.forEach(listener => listener());
        }
        return processor(response);
    }
}
