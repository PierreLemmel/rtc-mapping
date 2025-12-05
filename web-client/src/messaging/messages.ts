export type IncomingMessage = {
    type: string;
    data: string;
    clientId: string;
}

export type OutgoingMessage = {
    type: string;
    data: any;
}



export type ClientAddedData = {
    id: string;
    count: number;
}