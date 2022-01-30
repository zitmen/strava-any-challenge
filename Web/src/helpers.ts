import { useEffect, useState } from "react";
import { ApiException } from "./restClients";

export function getBaseUrl() {
    return document.getElementsByTagName('base')[0].getAttribute('href');
}

export function getHostUrl() {
    return window.location.origin.toString();
}

export const API_URL = process.env.REACT_APP_API_URL;

export function useApiCall<T>(apiCall: () => Promise<T>, initialData: T = undefined, deps: React.DependencyList = []): ApiCallState<T> {
    const [state, setState] = useState<ApiCallState<T>>({ data: initialData, loading: true, errorMessage: undefined });

    useEffect(() => {
        var isMounted = true;
        setState({ ...state, loading: true });

        apiCall()
            .then(data => {
                if (isMounted) {
                    setState({ ...state, data, loading: false, errorMessage: undefined })
                }
            })
            .catch(err => {
                const errorMessage = err instanceof ApiException
                    ? `${err.message}: ${err.response}`
                    : err.message;

                if (isMounted) {
                    setState({ ...state, loading: false, errorMessage });
                    return;
                }
            });

        return (() => { isMounted = false });
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, deps);

    return state;
}

export interface ApiCallState<T> {
    data: T | undefined,
    loading: boolean,
    errorMessage: string | undefined,
}

export function useCachedApiCallWithReload<T>(cacheKey: string, apiCall: () => Promise<T>): [ApiCallState<T>, () => void] {
    const [reloadCounter, setReloadCounter] = useState(0);
    const reload = () => { setReloadCounter(reloadCounter + 1) };
    const initialData = (localStorage.getItem(cacheKey) && JSON.parse(localStorage.getItem(cacheKey))) || undefined;
    const state = useApiCall<T>(apiCall, initialData, [reloadCounter]);
    // eslint-disable-next-line react-hooks/exhaustive-deps
    useEffect(() => state.data && localStorage.setItem(cacheKey, JSON.stringify(state.data)), [state.data]);
    return [state, reload];
}
