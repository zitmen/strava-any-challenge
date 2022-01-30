import { createContext, useContext, useState, useEffect } from 'react';
import { AuthClient } from './restClients';

const AuthContext = createContext<AuthorizationContext>({
    athleteId: undefined,

    login: _ => Promise.resolve(),
    logout: () => { },
});

export function useAuthContext() {
    return useContext(AuthContext);
}

export function AuthContextProvider({ children }) {
    const [state, setState] = useState({
        athleteId: localStorage.getItem('athleteId'),
    });

    const login = (code) => {
        async function asyncLogin() {
            const result = await new AuthClient().login({ code });
            
            localStorage.setItem('athleteId', result.athleteId);
            localStorage.setItem('accessToken', result.accessToken);
            if (result.isAdmin === true) {
                localStorage.setItem('isAdmin', "true");
            }

            setState({
                ...state,
                athleteId: result.athleteId,
            });
        }

        return asyncLogin();
    }

    const logout = () => {
        localStorage.removeItem('athleteId');
        localStorage.removeItem('accessToken');

        setState({
            ...state,
            athleteId: undefined,
        });
    }

    useEffect(() => {
        LogoutListeners.push(logout);
    });

    return (
        <AuthContext.Provider value={{ ...state, login, logout }}>
            {children}
        </AuthContext.Provider>
    );
}

export var LogoutListeners = [];

export interface AuthorizationContext {
    athleteId: string | undefined,
    login: (string) => Promise<void>,
    logout: () => void,
}