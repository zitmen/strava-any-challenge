import React, { Fragment } from 'react';
import { isAndroid as isDroid, getUA, CustomView } from 'react-device-detect';

function MobileCheck({ children }) {
    return (
        <Fragment>
            <div>
            {children}
            </div>
            <CustomView condition={shouldShowGooglePlayBadge()}>
                <p style={{ marginTop: '2em', display: 'flex', justifyContent: 'center', alignItems: 'center' }}>
                    Get the Android app for better experience<br />
                    <a href='https://play.google.com/store/apps/details?id=cz.zitmen.anychallenge&hl=cs&ah=KDiDwFb4jnxql-3TE04ssWxAGGI&pcampaignid=pcampaignidMKT-Other-global-all-co-prtnr-py-PartBadge-Mar2515-1'>
                        <img alt='Get it on Google Play' src='https://play.google.com/intl/en_us/badges/static/images/badges/en_badge_web_generic.png' width="100" />
                    </a>
                </p>
            </CustomView>
        </Fragment>
    );
}

export function shouldShowGooglePlayBadge(): boolean {
    return JSON.parse(localStorage.getItem('shouldShowGooglePlayBadge')) || figureOutIfShouldShowGooglePlayBadge();
}

function figureOutIfShouldShowGooglePlayBadge(): boolean {
    // ref: https://developer.chrome.com/docs/multidevice/user-agent/
    var show = isDroid && !getUA.includes("; wv; AnyChallenge");
    localStorage.setItem('shouldShowGooglePlayBadge', JSON.stringify(show));
    return show;
}

export function isAndroid(): boolean {
    return isDroid;
}

export default MobileCheck;