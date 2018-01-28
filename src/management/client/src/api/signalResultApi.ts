// -----------------------------------------------------------------------
// <copyright file="signalResultApi.ts" company="Microsoft Corporation">
//        Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

import * as moment from 'moment';

import baseUrl from './baseUrl';
import ApiError from './apiError';
import ListSmartSignalResultResponse from './models/ListSmartSignalResultResponse';
import ActiveDirectoryAuthenticatorFactory from '../factories/ActiveDirectoryAuthenticatorFactory';

/**
 * Gets a list of signals results without any flitering
 */
export async function getSignalsResultsAsync(): Promise<ListSmartSignalResultResponse> {
    const utcNowMinusDay = moment.utc().add(-1, 'days');
    const requestUrl = `${baseUrl}/api/signalResult?startTime=${utcNowMinusDay.format('DD/MM/YYYY HH:mm')}`;

    // Get the user AAD token
    let userAccessToken = await ActiveDirectoryAuthenticatorFactory.getActiveDirectoryAuthenticator()
                                                        .getResourceTokenAsync('https://management.core.windows.net/');

    const headers = new Headers();
    headers.set('Authorization', 'Bearer ' + userAccessToken);
    headers.set('Content-Type', 'application/json');

    const requestInit: RequestInit = {
        headers: headers,
        method: 'GET',
        mode: 'cors'
    };

    const response = await fetch(requestUrl, requestInit);
  
    if (response.ok) {
      return await response.json();
    } else {
      throw new ApiError(response.status, response.statusText);
    }
}