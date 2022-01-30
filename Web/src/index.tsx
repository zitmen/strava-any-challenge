import ReactDOM from 'react-dom';
import { BrowserRouter, Route, Routes } from 'react-router-dom';
import './index.css';
import AuthenticatedApp from './AuthenticatedApp';
import { getBaseUrl } from './helpers';
import { AuthContextProvider } from './AuthContext';
import Challenge from './Challenge';
import MobileCheck from './MobileCheck';
import Athlete from './Athlete';
import registerServiceWorker from './registerServiceWorker';
import Home from './Home';
import Admin from './Admin';
import AdminCreateChallenge from './AdminCreateChallenge';
import AdminDeleteChallenge from './AdminDeleteChallenge';
import AdminEditChallenge, { AdminEditChallengeForm } from './AdminEditChallenge';

ReactDOM.render(
    <MobileCheck>
        <BrowserRouter basename={getBaseUrl()}>
            <AuthContextProvider>
                <AuthenticatedApp>
                    <Routes>
                        <Route path='/challenge/:challengeId/athlete/:athleteId' element={<Athlete />} />
                        <Route path='/challenge/:challengeId' element={<Challenge />} />
                        <Route path='/admin' element={<Admin />} />
                        <Route path='/admin/create' element={<AdminCreateChallenge />} />
                        <Route path='/admin/edit' element={<AdminEditChallenge />} />
                        <Route path='/admin/edit/:challengeId' element={<AdminEditChallengeForm />} />
                        <Route path='/admin/delete' element={<AdminDeleteChallenge />} />
                        <Route path='/*' element={<Home />} />
                    </Routes>
                </AuthenticatedApp>
            </AuthContextProvider>
        </BrowserRouter>
    </MobileCheck>,
    document.getElementById('root'));

registerServiceWorker();
